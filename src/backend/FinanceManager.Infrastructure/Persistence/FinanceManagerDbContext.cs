using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Authentication;
using FinanceManager.Domain.Transactions;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

public sealed class FinanceManagerDbContext(DbContextOptions<FinanceManagerDbContext> options)
    : IdentityDbContext<Owner, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<OwnerSession> OwnerSessions => Set<OwnerSession>();

    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new FinancialAccountConfiguration());
        builder.ApplyConfiguration(new FinancialTransactionConfiguration());
        builder.Entity<FinancialTransactionAudit>(audit =>
        {
            audit.ToTable("FinancialTransactionAudits");
            audit.HasKey(row => row.Id);
            audit.Property(row => row.Id).ValueGeneratedOnAdd();
            audit.Property(row => row.Action).HasConversion<string>().HasMaxLength(8);
            audit.HasOne<FinancialTransaction>().WithMany()
                .HasForeignKey(row => new { row.OwnerId, row.TransactionId })
                .HasPrincipalKey(row => new { row.OwnerId, row.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<FinancialAccountAudit>(audit =>
        {
            audit.ToTable("FinancialAccountAudits");
            audit.Property(row => row.Action).HasConversion<string>().HasMaxLength(12);
            audit.HasOne<FinancialAccount>().WithMany().HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Owner>().HasIndex(owner => owner.ProvisioningSlot)
            .IsUnique().HasDatabaseName(IdentityOwnerAdapter.ProvisioningIndex);
        builder.Entity<OwnerSession>(session =>
        {
            session.HasKey(row => row.Id);
            session.Property(row => row.Id).ValueGeneratedNever();
            session.HasOne<Owner>().WithMany().HasForeignKey(row => row.OwnerId);
        });
    }
}
