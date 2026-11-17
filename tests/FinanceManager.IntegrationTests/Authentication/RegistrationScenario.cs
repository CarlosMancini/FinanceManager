using FinanceManager.IntegrationTests.Fixtures;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;

namespace FinanceManager.IntegrationTests.Authentication;

internal sealed class RegistrationScenario : IAsyncDisposable
{
    public SqlServerTestDatabase Database { get; } = new();
    public const string Email = "titular@example.test";
    public const string Password = "Somente-Teste!1234";

    public static async Task<RegistrationScenario> EmptyAsync()
    {
        var scenario = new RegistrationScenario();
        try
        {
            await scenario.Database.MigrateAsync();
            return scenario;
        }
        catch
        {
            await scenario.DisposeAsync();
            throw;
        }
    }

    public static async Task<RegistrationScenario> CompletedAsync(CancellationToken cancellationToken)
    {
        var scenario = await EmptyAsync();
        try
        {
            await using var context = scenario.Database.CreateContext();
            var id = Guid.NewGuid();
            var owner = new Owner
            {
                Id = id, ProvisioningSlot = 1, UserName = id.ToString("N"),
                NormalizedUserName = id.ToString("N").ToUpperInvariant(),
                FullName = "Pessoa de Teste", BirthDate = new DateOnly(1990, 1, 1),
                Email = Email, NormalizedEmail = Email.ToUpperInvariant(), PhoneNumber = "11999999999",
                RegistrationCompletedAtUtc = DateTimeOffset.UtcNow
            };
            owner.PasswordHash = new PasswordHasher<Owner>().HashPassword(owner, Password);
            context.Users.Add(owner);
            await context.SaveChangesAsync(cancellationToken);
            return scenario;
        }
        catch
        {
            await scenario.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => Database.DisposeAsync();
}
