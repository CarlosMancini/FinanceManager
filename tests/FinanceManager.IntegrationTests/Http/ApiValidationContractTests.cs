using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using FinanceManager.IntegrationTests.Authentication;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.IntegrationTests.Http;

[ApiController]
[AllowAnonymous]
[Route("api/test-validation")]
public sealed class ValidationTestController : ControllerBase
{
    [HttpPost]
    public IActionResult Post(ValidationTestRequest request) => NoContent();
}

public sealed record ValidationTestRequest([Required] string Name, DateOnly BirthDate);

public sealed class ApiValidationContractTests
{
    [Fact]
    public async Task AnonymousMutationRequiresCsrf()
    {
        await using var root = new FinanceManagerApiFactory("Server=(localdb)\\MSSQLLocalDB;Database=Unused;Integrated Security=true");
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ValidationTestController).Assembly)));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsync("/api/test-validation", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"birthDate\":\"2020-01-01\"}")]
    [InlineData("{\"name\":\"Pessoa\",\"birthDate\":\"2020-02-31\"}")]
    [InlineData("{\"name\":\"Pessoa\",\"birthDate\":\"2020-01-01\",\"unknown\":\"segredo\"}")]
    public async Task InvalidInputReturnsFieldErrorsWithoutEchoingValues(string json)
    {
        var token = TestContext.Current.CancellationToken;
        await using var root = new FinanceManagerApiFactory("Server=(localdb)\\MSSQLLocalDB;Database=Unused;Integrated Security=true");
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ValidationTestController).Assembly)));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test-validation");
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(token);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("VALIDATION_FAILED", document.RootElement.GetProperty("code").GetString());
        Assert.NotEmpty(document.RootElement.GetProperty("errors").EnumerateObject());
        Assert.DoesNotContain("segredo", body);
    }
}
