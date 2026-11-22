using FinanceManager.Infrastructure.Authentication;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;

namespace FinanceManager.IntegrationTests.Authentication;

internal sealed class OwnerProfileScenario : IAsyncDisposable
{
    public const string Password = "Somente-Teste!1234";
    public SqlServerTestDatabase Database { get; } = new();
    public Guid FirstOwnerId { get; private set; }
    public Guid SecondOwnerId { get; private set; }
    public FinanceManagerApiFactory Factory { get; private set; } = null!;

    public static async Task<OwnerProfileScenario> CreateAsync(CancellationToken cancellationToken)
    {
        var scenario = new OwnerProfileScenario();
        try
        {
            await scenario.Database.MigrateAsync();
            await using var database = scenario.Database.CreateContext();
            var first = CreateOwner("primeira@example.test");
            var second = CreateOwner("segunda@example.test");
            scenario.FirstOwnerId = first.Id;
            scenario.SecondOwnerId = second.Id;
            database.Users.AddRange(first, second);
            await database.SaveChangesAsync(cancellationToken);
            scenario.Factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
            return scenario;
        }
        catch
        {
            await scenario.DisposeAsync();
            throw;
        }
    }

    private static Owner CreateOwner(string email)
    {
        var id = Guid.NewGuid();
        var owner = new Owner
        {
            Id = id, ProvisioningSlot = 1, UserName = id.ToString("N"),
            NormalizedUserName = id.ToString("N").ToUpperInvariant(), FullName = "Pessoa de Teste",
            BirthDate = new DateOnly(1990, 1, 1), Email = email, NormalizedEmail = email.ToUpperInvariant(),
            PhoneNumber = "11999999999", RegistrationCompletedAtUtc = DateTimeOffset.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(), LockoutEnabled = true
        };
        owner.PasswordHash = new PasswordHasher<Owner>().HashPassword(owner, Password);
        return owner;
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await Database.DisposeAsync();
    }
}
