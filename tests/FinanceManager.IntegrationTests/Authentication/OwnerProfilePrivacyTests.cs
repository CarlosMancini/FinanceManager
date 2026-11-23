using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfilePrivacyTests
{
    [Fact]
    public async Task LogsDoNotContainProfileValuesOrPasswords()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        var capture = new CapturingLoggerProvider();
        await using var factory = scenario.Factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(capture)));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        capture.Messages.Clear();
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { fullName = "Nome Sintetico Privado" }, originalValues = new { fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(capture.Messages);
        var messages = string.Join('\n', capture.Messages);
        Assert.DoesNotContain("Nome Sintetico Privado", messages, StringComparison.Ordinal);
        Assert.DoesNotContain("Pessoa de Teste", messages, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnerProfileScenario.Password, messages, StringComparison.Ordinal);
        Assert.DoesNotContain("Outra-Senha!1234", messages, StringComparison.Ordinal);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
        public void Dispose() { }
        private sealed class CaptureLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }

    [Fact]
    public async Task ProfileAndConflictReturnNoCredentialsOrCachedValues()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var get = await client.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.True(get.Headers.CacheControl?.NoStore);
        using var profile = JsonDocument.Parse(await get.Content.ReadAsStringAsync(token));
        Assert.Equal(new[] { "birthDate", "email", "fullName", "nickname", "phoneNumber" }, profile.RootElement.EnumerateObject().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { fullName = "Rascunho Privado" }, originalValues = new { fullName = "Original Obsoleto" } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var conflict = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.True(conflict.Headers.CacheControl?.NoStore);
        var body = await conflict.Content.ReadAsStringAsync(token);
        Assert.DoesNotContain("Rascunho Privado", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Original Obsoleto", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Pessoa de Teste", body, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnerProfileScenario.Password, body, StringComparison.Ordinal);
    }
}
