using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class VoucherHeaderConfiguration : IEntityTypeConfiguration<VoucherHeader>
{
    public void Configure(EntityTypeBuilder<VoucherHeader> builder)
    {
        builder.HasKey(item => item.Id);

        builder.Property(item => item.SequenceNumber)
            .IsRequired();

        builder.Property(item => item.VoucherNumber);

        builder.Property(item => item.DocumentNumber)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasMaxLength(600);

        builder.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(VoucherStatus.Draft)
            .IsRequired();

        builder.Property(item => item.PostingStatus)
            .HasMaxLength(20)
            .HasDefaultValue(PostingStatus.Draft)
            .IsRequired();

        builder.Property(item => item.TotalDebits)
            .HasPrecision(18, 2);

        builder.Property(item => item.TotalCredits)
            .HasPrecision(18, 2);

        builder.HasIndex(item => new { item.FiscalYearId, item.SequenceNumber })
            .IsUnique();

        builder.HasIndex(item => new { item.FiscalYearId, item.VoucherNumber });

        builder.HasIndex(item => item.DocumentNumber);

        builder.HasIndex(item => item.VoucherDate);
        builder.HasIndex(item => new { item.Status, item.VoucherDate, item.TotalDebits, item.TotalCredits });
        builder.HasIndex(item => new { item.FiscalYearId, item.VoucherDate });
        builder.HasIndex(item => new { item.JournalTypeId, item.VoucherDate });
        builder.HasIndex(item => item.PostingStatus);

        builder.HasOne(item => item.FiscalYear)
            .WithMany(item => item.VoucherHeaders)
            .HasForeignKey(item => item.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.JournalType)
            .WithMany(item => item.Vouchers)
            .HasForeignKey(item => item.JournalTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.ReversalOfVoucherHeader)
            .WithOne(item => item.ReversedByVoucherHeader)
            .HasForeignKey<VoucherHeader>(item => item.ReversalOfVoucherHeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.Lines)
            .WithOne(item => item.VoucherHeader)
            .HasForeignKey(item => item.VoucherHeaderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
