using FinanceManager.Domain.Bills;
using FinanceManager.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class BillSettlementConfiguration : IEntityTypeConfiguration<BillSettlement>
{
    internal const string ActiveBillIndex = "UX_BillSettlements_OwnerId_BillId_Active";
    internal const string ActiveTransactionIndex = "UX_BillSettlements_OwnerId_TransactionId_Active";

    public void Configure(EntityTypeBuilder<BillSettlement> settlement)
    {
        settlement.ToTable("BillSettlements", table =>
            table.HasCheckConstraint("CK_BillSettlements_Origin", "[Origin] IN (N'LinkedExpense', N'CreatedExpense')"));
        settlement.HasKey(row => row.Id);
        settlement.HasAlternateKey(row => new { row.OwnerId, row.Id });
        settlement.Property(row => row.Id).ValueGeneratedNever();
        settlement.HasOne<Bill>().WithMany()
            .HasForeignKey(row => new { row.OwnerId, row.BillId })
            .HasPrincipalKey(row => new { row.OwnerId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        settlement.HasOne<FinancialTransaction>().WithMany()
            .HasForeignKey(row => new { row.OwnerId, row.TransactionId })
            .HasPrincipalKey(row => new { row.OwnerId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        settlement.Property(row => row.Origin).HasConversion<string>().HasMaxLength(16);
        settlement.Ignore(row => row.IsActive);
        settlement.HasIndex(row => new { row.OwnerId, row.BillId }).IsUnique()
            .HasFilter("[ReversedAtUtc] IS NULL").HasDatabaseName(ActiveBillIndex);
        settlement.HasIndex(row => new { row.OwnerId, row.TransactionId }).IsUnique()
            .HasFilter("[ReversedAtUtc] IS NULL").HasDatabaseName(ActiveTransactionIndex);
    }
}
