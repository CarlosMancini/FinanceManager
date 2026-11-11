using FinanceManager.Domain.Bills;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    internal const string VersionProperty = "Version";

    public void Configure(EntityTypeBuilder<Bill> bill)
    {
        bill.ToTable("Bills", table =>
        {
            table.HasCheckConstraint("CK_Bills_Amount", "[Amount] >= 0.01 AND [Amount] <= 99999999999999999.99");
            table.HasCheckConstraint("CK_Bills_Currency", "[Currency] = N'BRL'");
        });
        bill.HasKey(row => row.Id);
        bill.HasAlternateKey(row => new { row.OwnerId, row.Id });
        bill.Property(row => row.Id).ValueGeneratedNever();
        bill.HasOne<Owner>().WithMany().HasForeignKey(row => row.OwnerId).OnDelete(DeleteBehavior.Restrict);
        bill.Property(row => row.Description).IsRequired().HasMaxLength(200);
        bill.Property(row => row.Amount).HasPrecision(19, 2);
        bill.Property(row => row.DueDate).HasColumnType("date");
        bill.Property(row => row.Currency).IsRequired().HasMaxLength(3);
        bill.Property<byte[]>(VersionProperty).IsRowVersion();
        bill.HasIndex(row => new { row.OwnerId, row.DueDate, row.Id });
    }
}

internal enum BillAuditAction { Created, Edited, Canceled, Settled, Reversed }

internal sealed class BillAudit
{
    public long Id { get; private set; }
    public Guid OwnerId { get; init; }
    public Guid BillId { get; init; }
    public Guid? SettlementId { get; init; }
    public Guid? TransactionId { get; init; }
    public BillAuditAction Action { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
}
