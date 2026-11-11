using FinanceManager.Application.Bills;
using FinanceManager.Domain.Bills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class BillSettlementRequestConfiguration : IEntityTypeConfiguration<BillSettlementRequest>
{
    public void Configure(EntityTypeBuilder<BillSettlementRequest> request)
    {
        request.ToTable("BillSettlementRequests");
        request.HasKey(row => new { row.OwnerId, row.IdempotencyKey });
        request.Property(row => row.Fingerprint).IsRequired().HasMaxLength(32).IsFixedLength();
        request.Property(row => row.Origin).HasConversion<string>().HasMaxLength(16);
        request.HasOne<BillSettlement>().WithMany()
            .HasForeignKey(row => new { row.OwnerId, row.SettlementId })
            .HasPrincipalKey(row => new { row.OwnerId, row.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
