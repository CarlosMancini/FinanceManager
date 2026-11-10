using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Fixtures;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    public SqlServerTestDatabase()
    {
        var settings = Environment.GetEnvironmentVariable("ConnectionStrings__FinanceManagerTests")
            ?? throw new InvalidOperationException("Configure ConnectionStrings__FinanceManagerTests para a suíte SQL Server.");
        var connection = new SqlConnectionStringBuilder(settings)
        {
            InitialCatalog = $"FinanceManagerTest_{Guid.NewGuid():N}",
            Pooling = false
        };
        ConnectionString = connection.ConnectionString;
    }

    public string ConnectionString { get; }

    public FinanceManagerDbContext CreateContext() => new(
        new DbContextOptionsBuilder<FinanceManagerDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task MigrateAsync()
    {
        await using var database = CreateContext();
        await database.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await using var database = CreateContext();
        await database.Database.EnsureDeletedAsync();
    }
}
