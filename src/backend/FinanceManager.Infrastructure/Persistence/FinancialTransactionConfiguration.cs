using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    internal const string RequestIndex = "UX_FinancialTransactions_OwnerId_CreationRequestId";
    internal const string HistoryIndex = "IX_FinancialTransactions_OwnerId_TransactionDate_Id";
    internal const string VersionProperty = "Version";

    public void Configure(EntityTypeBuilder<FinancialTransaction> transaction)
    {
        transaction.ToTable("FinancialTransactions", table =>
        {
            table.HasCheckConstraint("CK_FinancialTransactions_Amount", "[Amount] >= 0.01 AND [Amount] <= 99999999999999999.99");
            table.HasCheckConstraint("CK_FinancialTransactions_Currency", "[Currency] = N'BRL'");
            table.HasCheckConstraint("CK_FinancialTransactions_Kind", "[Kind] IN (N'Income', N'Expense')");
            table.HasCheckConstraint("CK_FinancialTransactions_Source", "[Source] = N'Manual'");
            table.HasCheckConstraint("CK_FinancialTransactions_Status", "[Status] IN (N'Active', N'Canceled')");
            table.HasCheckConstraint("CK_FinancialTransactions_Cancellation", "([Status] = N'Canceled' AND [CanceledAtUtc] IS NOT NULL) OR ([Status] = N'Active' AND [CanceledAtUtc] IS NULL)");
        });
        transaction.HasKey(row => row.Id);
        transaction.HasAlternateKey(row => new { row.OwnerId, row.Id });
        transaction.Property(row => row.Id).ValueGeneratedNever();
        transaction.HasOne<FinancialAccount>().WithMany()
            .HasForeignKey(row => new { row.OwnerId, row.AccountId })
            .HasPrincipalKey(row => new { row.OwnerId, row.Id })
            .OnDelete(DeleteBehavior.Restrict);
        transaction.Property(row => row.Kind).HasConversion<string>().HasMaxLength(8);
        transaction.Property(row => row.Amount).HasPrecision(19, 2);
        transaction.Property(row => row.TransactionDate).HasColumnType("date");
        transaction.Property(row => row.Description).IsRequired().HasMaxLength(200);
        transaction.Property(row => row.Currency).IsRequired().HasMaxLength(3);
        transaction.Property(row => row.Source).HasConversion<string>().HasMaxLength(8);
        transaction.Property(row => row.Status).HasConversion<string>().HasMaxLength(8);
        transaction.Property(row => row.CreationFingerprint).IsRequired().HasMaxLength(32).IsFixedLength();
        transaction.Property<byte[]>(VersionProperty).IsRowVersion();
        transaction.HasIndex(row => new { row.OwnerId, row.CreationRequestId }).IsUnique().HasDatabaseName(RequestIndex);
        transaction.HasIndex(row => new { row.OwnerId, row.TransactionDate, row.Id }).IsDescending(false, true, true)
            .HasDatabaseName(HistoryIndex);

    }
}

internal enum TransactionAuditAction { Created, Edited, Canceled }

internal sealed class FinancialTransactionAudit
{
    public long Id { get; init; }
    public Guid OwnerId { get; init; }
    public Guid TransactionId { get; init; }
    public TransactionAuditAction Action { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
}
