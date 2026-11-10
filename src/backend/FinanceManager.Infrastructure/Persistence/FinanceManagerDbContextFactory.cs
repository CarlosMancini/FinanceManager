using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceManager.Infrastructure.Persistence;

public sealed class FinanceManagerDbContextFactory : IDesignTimeDbContextFactory<FinanceManagerDbContext>
{
    public FinanceManagerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FinanceManager")
            ?? throw new InvalidOperationException("Configure a conexão local do FinanceManager.");

        return new FinanceManagerDbContext(
            new DbContextOptionsBuilder<FinanceManagerDbContext>().UseSqlServer(connectionString).Options);
    }
}
