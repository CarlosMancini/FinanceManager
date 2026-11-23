using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfilePasswordContractTests
{
    [Fact]
    public async Task PasswordOnlyChangeIgnoresConcurrentChangesToOmittedFields()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var first = scenario.Factory.CreateHttpsClient(); using var second = scenario.Factory.CreateHttpsClient();
        using var login1 = await AuthTestClient.LoginAsync(first, token, email: "primeira@example.test");
        using var login2 = await AuthTestClient.LoginAsync(second, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login1.StatusCode); Assert.Equal(HttpStatusCode.NoContent, login2.StatusCode);
        var csrf1 = await AuthTestClient.GetCsrfAsync(first, token); var csrf2 = await AuthTestClient.GetCsrfAsync(second, token);
        using var edit = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        { Content = JsonContent.Create(new { changes = new { fullName = "Novo Nome" }, originalValues = new { fullName = "Pessoa de Teste" } }) };
        edit.Headers.Add("X-XSRF-TOKEN", csrf1);
        using var edited = await first.SendAsync(edit, token);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        { Content = JsonContent.Create(new { changes = new { }, originalValues = new { }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Senha-Nova!1234", confirmPassword = "Senha-Nova!1234" } }) };
        request.Headers.Add("X-XSRF-TOKEN", csrf2);
        using var response = await second.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var newClient = scenario.Factory.CreateHttpsClient();
        using var newLogin = await AuthTestClient.LoginAsync(newClient, token, email: "primeira@example.test", password: "Senha-Nova!1234");
        Assert.Equal(HttpStatusCode.NoContent, newLogin.StatusCode);
    }

    [Fact]
    public async Task PasswordErrorsAreNamedAndCurrentPasswordFailureKeepsSession()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        foreach (var (current, next, confirm, field) in new[] {
            ("", "Senha-Nova!1234", "Senha-Nova!1234", "currentPassword"),
            (OwnerProfileScenario.Password, "curta", "curta", "newPassword"),
            (OwnerProfileScenario.Password, "Senha-Nova!1234", "Senha-Outra!123", "confirmPassword"),
            ("senha-incorreta", "Senha-Nova!1234", "Senha-Nova!1234", "currentPassword") })
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
            { Content = JsonContent.Create(new { changes = new { }, originalValues = new { }, passwordChange = new { currentPassword = current, newPassword = next, confirmPassword = confirm } }) };
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            using var response = await client.SendAsync(request, token);
            await AuthTestClient.AssertProblemAsync(response, 400, "VALIDATION_FAILED", token);
            var content = await response.Content.ReadAsStringAsync(token);
            using var json = JsonDocument.Parse(content);
            Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _));
            Assert.DoesNotContain(OwnerProfileScenario.Password, content, StringComparison.Ordinal);
            Assert.DoesNotContain("Senha-Nova!1234", content, StringComparison.Ordinal);
        }
        using var session = await client.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }
}
