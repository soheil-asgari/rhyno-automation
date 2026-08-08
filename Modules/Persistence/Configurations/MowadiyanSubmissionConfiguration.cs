using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.Persistence.Configurations;

public sealed class MowadiyanSubmissionConfiguration : IEntityTypeConfiguration<MowadiyanSubmission>
{
    public void Configure(EntityTypeBuilder<MowadiyanSubmission> builder)
    {
        builder.Property(s => s.StatusText).HasMaxLength(40);
        builder.Property(s => s.PayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(s => s.SignedEnvelope).HasColumnType("nvarchar(max)");
        builder.Property(s => s.TaxReferenceId).HasMaxLength(100);
        builder.Property(s => s.FiscalId).HasMaxLength(100);
        builder.Property(s => s.ResponseJson).HasColumnType("nvarchar(max)");
        builder.Property(s => s.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(s => s.InvoiceRecord)
            .WithMany(r => r.MowadiyanSubmissions)
            .HasForeignKey(s => s.InvoiceRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.InvoiceRecordId).HasDatabaseName("IX_MowadiyanSubmission_InvoiceRecordId");
        builder.HasIndex(s => s.Status).HasDatabaseName("IX_MowadiyanSubmission_Status");
        builder.HasIndex(s => s.TaxReferenceId).HasDatabaseName("IX_MowadiyanSubmission_TaxReferenceId");
    }
}

public sealed class MowadiyanSettingsConfiguration : IEntityTypeConfiguration<MowadiyanSettings>
{
    public void Configure(EntityTypeBuilder<MowadiyanSettings> builder)
    {
        builder.Property(s => s.ClientId).HasMaxLength(200).IsRequired();
        builder.Property(s => s.MemorySecret).HasMaxLength(500);
        builder.Property(s => s.PrivateKeyPath).HasMaxLength(500);
        builder.Property(s => s.PrivateKeyPassword).HasMaxLength(200);
        builder.Property(s => s.EconomicCode).HasMaxLength(50);
        builder.Property(s => s.NationalId).HasMaxLength(20);
        builder.Property(s => s.BaseUrl).HasMaxLength(300);
    }
}