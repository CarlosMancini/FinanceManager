using FinanceManager.Infrastructure.Authentication;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class RegistrationIdentityValidationTests
{
    [Fact]
    public async Task IdentityAcceptsUnicodeLengthAndLongEmail()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.EmptyAsync();
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Owner>>();
        var id = Guid.NewGuid();
        var owner = new Owner { Id = id, ProvisioningSlot = 1, UserName = id.ToString("N"), FullName = "Pessoa", BirthDate = new DateOnly(1990, 1, 1), Email = new string('a', 260) + "@b", PhoneNumber = "1234567890", RegistrationCompletedAtUtc = factory.Clock.GetUtcNow() };
        var result = await users.CreateAsync(owner, "Aa1!" + new string('a', 123) + "😀");
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Code)));
        Assert.True(await users.CheckPasswordAsync(owner, "Aa1!" + new string('a', 123) + "😀"));
        Assert.False((await users.PasswordValidators.Single().ValidateAsync(users, owner, "Aa1!" + new string('a', 124) + "😀")).Succeeded);
        Assert.False((await users.PasswordValidators.Single().ValidateAsync(users, owner, "Aa١!aaaaaaaa")).Succeeded);
        Assert.NotEqual(Guid.Empty, owner.Id);
        token.ThrowIfCancellationRequested();
    }

    [Fact]
    public async Task ExistingLegacyPasswordCanBeVerifiedWithoutApplyingNewPasswordPolicy()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var context = scenario.Database.CreateContext()) await context.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Owner>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<Owner>>();
        var owner = (await users.FindByNameAsync(LegacyRegistrationScenario.Username))!;
        owner.PasswordHash = users.PasswordHasher.HashPassword(owner, "curta");
        Assert.True((await users.UpdateAsync(owner)).Succeeded);
        Assert.True((await signIn.CheckPasswordSignInAsync(owner, "curta", true)).Succeeded);
    }
}
