using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Authentication;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new FinancialAccountConfiguration());
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
