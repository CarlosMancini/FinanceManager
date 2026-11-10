using FinanceManager.Application.Accounts;
using FinanceManager.Domain.Accounts;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    internal const string NameIndex = "UX_FinancialAccounts_OwnerId_NormalizedName";
    internal const string VersionProperty = "Version";

    public void Configure(EntityTypeBuilder<FinancialAccount> account)
    {
        account.ToTable("FinancialAccounts", table =>
        {
            table.HasCheckConstraint("CK_FinancialAccounts_Currency", "[Currency] = N'BRL'");
            table.HasCheckConstraint("CK_FinancialAccounts_Status", "[Status] IN (N'Active', N'Archived')");
            table.HasCheckConstraint("CK_FinancialAccounts_Type", "[Type] IN (N'Checking', N'Savings', N'Wallet')");
        });
        account.HasKey(row => row.Id);
        account.Property(row => row.Id).ValueGeneratedNever();
        account.HasOne<Owner>().WithMany().HasForeignKey(row => row.OwnerId).OnDelete(DeleteBehavior.Restrict);
        account.Property(row => row.Name).IsRequired().HasMaxLength(120);
        account.Property(row => row.NormalizedName).IsRequired().HasMaxLength(450).UseCollation("Latin1_General_100_BIN2");
        account.HasIndex(row => new { row.OwnerId, row.NormalizedName }).IsUnique().HasDatabaseName(NameIndex);
        account.Property(row => row.Type).HasConversion<string>().HasMaxLength(12);
        account.Property(row => row.Status).HasConversion<string>().HasMaxLength(12);
        account.Property(row => row.Currency).HasMaxLength(3).IsRequired();
        account.Property<byte[]>(VersionProperty).IsRowVersion();
    }
}

internal sealed class FinancialAccountAudit
{
    public long Id { get; private set; }
    public Guid OwnerId { get; init; }
    public Guid AccountId { get; init; }
    public AccountChange Action { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
}
