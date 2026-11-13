using System.Net;
using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;
using FinanceManager.Infrastructure.Authentication;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.IntegrationTests.Authentication;

[ApiController]
[AllowAnonymous]
[Route("api/test-session")]
public sealed class SessionFixtureController : ControllerBase
{
    [HttpGet("{ownerId:guid}/{sessionId:guid}")]
    public async Task<IActionResult> Get(Guid ownerId, Guid sessionId)
    {
        await HttpContext.SignInAsync(OwnerTicketStore.Scheme, OwnerTicketStore.CreatePrincipal(sessionId, ownerId, "sintetico"));
        return NoContent();
    }
}

public sealed class IncompleteOwnerAccessTests
{
    [Fact]
    public async Task LegacySessionCannotBeReconstructedOrAdmitted()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        await using var testFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(SessionFixtureController).Assembly)));
        using var client = testFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        using var cookie = await client.GetAsync($"/api/test-session/{scenario.OwnerId}/{scenario.SessionId}", token);
        Assert.Equal(HttpStatusCode.NoContent, cookie.StatusCode);
        await using var scope = testFactory.Services.CreateAsyncScope();
        var tickets = scope.ServiceProvider.GetRequiredService<OwnerTicketStore>();
        var sessions = scope.ServiceProvider.GetRequiredService<IOwnerSessionStore>();
        Assert.Null(await tickets.RetrieveAsync(scenario.SessionId.ToString(), token));
        Assert.False(await sessions.AdmitAsync(scenario.SessionId, factory.Clock.GetUtcNow(), token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sessions.CreateAsync(new OwnerSession(scenario.OwnerId, factory.Clock.GetUtcNow()), token));
        foreach (var route in new[] { "/api/accounts", "/api/transactions", "/api/bills" })
        {
            using var response = await client.GetAsync(route, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task CompletedOwnerSessionCanBeReconstructedAndAdmitted()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.CompletedAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var client = factory.CreateHttpsClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IOwnerSessionStore>();
        await using var db = scenario.Database.CreateContext();
        var owner = await db.Users.SingleAsync(token);
        var session = new OwnerSession(owner.Id, factory.Clock.GetUtcNow());
        await sessions.CreateAsync(session, token);
        Assert.True(await sessions.AdmitAsync(session.Id, factory.Clock.GetUtcNow(), token));
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<OwnerTicketStore>().RetrieveAsync(session.Id.ToString(), token));
        factory.Clock.Advance(TimeSpan.FromMinutes(30));
        Assert.False(await sessions.AdmitAsync(session.Id, factory.Clock.GetUtcNow(), token));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<OwnerTicketStore>().RetrieveAsync(session.Id.ToString(), token));
    }
}
