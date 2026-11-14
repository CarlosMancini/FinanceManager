using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Authentication;
using FinanceManager.Domain.Bills;
using FinanceManager.Domain.Transactions;
using FinanceManager.Application.Bills;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

public sealed class FinanceManagerDbContext(DbContextOptions<FinanceManagerDbContext> options)
    : IdentityDbContext<Owner, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<OwnerSession> OwnerSessions => Set<OwnerSession>();
    public DbSet<RegistrationAuthorization> RegistrationAuthorizations => Set<RegistrationAuthorization>();
    public DbSet<RegistrationAudit> RegistrationAudits => Set<RegistrationAudit>();

    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillSettlement> BillSettlements => Set<BillSettlement>();
    public DbSet<BillSettlementRequest> BillSettlementRequests => Set<BillSettlementRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new FinancialAccountConfiguration());
        builder.ApplyConfiguration(new FinancialTransactionConfiguration());
        builder.ApplyConfiguration(new BillConfiguration());
        builder.ApplyConfiguration(new BillSettlementConfiguration());
        builder.ApplyConfiguration(new BillSettlementRequestConfiguration());
        builder.Entity<BillAudit>(audit =>
        {
            audit.ToTable("BillAudits");
            audit.HasKey(row => row.Id);
            audit.Property(row => row.Id).ValueGeneratedOnAdd();
            audit.Property(row => row.Action).HasConversion<string>().HasMaxLength(10);
            audit.HasOne<Bill>().WithMany().HasForeignKey(row => new { row.OwnerId, row.BillId })
                .HasPrincipalKey(row => new { row.OwnerId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        });
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
        builder.Entity<Owner>(owner =>
        {
            owner.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Owners_ProvisioningSlot", "[ProvisioningSlot] = 1");
                table.HasCheckConstraint("CK_Owners_RegistrationProfile", "[RegistrationCompletedAtUtc] IS NULL OR ([FullName] IS NOT NULL AND LEN(LTRIM(RTRIM([FullName]))) > 0 AND [BirthDate] IS NOT NULL AND [Email] IS NOT NULL AND LEN(LTRIM(RTRIM([Email]))) > 0 AND [NormalizedEmail] IS NOT NULL AND LEN(LTRIM(RTRIM([NormalizedEmail]))) > 0 AND [PhoneNumber] IS NOT NULL AND LEN(LTRIM(RTRIM([PhoneNumber]))) > 0)");
            });
            owner.HasIndex(row => row.ProvisioningSlot).IsUnique().HasDatabaseName(IdentityOwnerAdapter.ProvisioningIndex);
            var emailProperty = owner.Property(row => row.Email).HasColumnType("nvarchar(max)");
            emailProperty.Metadata.SetMaxLength(null);
            var normalizedEmailProperty = owner.Property(row => row.NormalizedEmail).HasColumnType("nvarchar(max)").UseCollation("Latin1_General_100_BIN2");
            normalizedEmailProperty.Metadata.SetMaxLength(null);
            var emailIndex = owner.Metadata.FindIndex(normalizedEmailProperty.Metadata);
            if (emailIndex is not null) owner.Metadata.RemoveIndex(emailIndex);
        });
        builder.Entity<RegistrationAuthorization>(authorization =>
        {
            authorization.ToTable("RegistrationAuthorizations", table =>
            {
                table.HasCheckConstraint("CK_RegistrationAuthorizations_Expiry", "[ExpiresAtUtc] = DATEADD(minute, 10, [VerifiedAtUtc])");
                table.HasCheckConstraint("CK_RegistrationAuthorizations_Revocation", "[RevokedAtUtc] IS NULL OR [RevokedAtUtc] >= [VerifiedAtUtc]");
            });
            authorization.HasKey(row => row.Id);
            authorization.Property(row => row.Id).ValueGeneratedNever();
            authorization.Property(row => row.TokenHash).HasColumnType("binary(32)").IsRequired();
            authorization.HasIndex(row => row.TokenHash).IsUnique();
            authorization.HasOne<Owner>().WithMany().HasForeignKey(row => row.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RegistrationAudit>(audit =>
        {
            audit.ToTable("RegistrationAudits", table => table.HasCheckConstraint("CK_RegistrationAudits_Action", "[Action] IN (N'Registered', N'RegistrationCompleted')"));
            audit.HasKey(row => row.Id);
            audit.Property(row => row.Id).ValueGeneratedOnAdd();
            audit.Property(row => row.Action).HasConversion<string>().HasMaxLength(32);
            audit.HasOne<Owner>().WithMany().HasForeignKey(row => row.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<OwnerSession>(session =>
        {
            session.HasKey(row => row.Id);
            session.Property(row => row.Id).ValueGeneratedNever();
            session.HasOne<Owner>().WithMany().HasForeignKey(row => row.OwnerId);
        });
    }
}
