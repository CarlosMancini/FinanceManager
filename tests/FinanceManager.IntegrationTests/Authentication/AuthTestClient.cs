using System.Net.Http.Json;

namespace FinanceManager.IntegrationTests.Authentication;

internal static class AuthTestClient
{
    public const string Username = "titular";
    public const string Email = "titular@example.test";
    public const string Password = "Somente-Teste!1234";
    public const string CookieName = "__Host-FinanceManager";

    public static async Task<string> GetCsrfAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/auth/csrf", cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie.Split(';')[0]["XSRF-TOKEN=".Length..]);
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, CancellationToken cancellationToken,
        string password = Password, string email = Email)
    {
        var csrf = await GetCsrfAsync(client, cancellationToken);
        return await PostAsync(client, "/api/auth/login", csrf, new { email, password }, cancellationToken);
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string? csrf,
        object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (csrf is not null) request.Headers.Add("X-XSRF-TOKEN", csrf);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request, cancellationToken);
    }

    public static async Task AssertProblemAsync(HttpResponseMessage response, int status, string code, CancellationToken cancellationToken)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var content = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(code, content.RootElement.GetProperty("code").GetString());
    }
}
