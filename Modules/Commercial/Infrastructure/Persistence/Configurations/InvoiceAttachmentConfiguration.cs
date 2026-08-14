using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Commercial.Domain;

namespace OfficeAutomation.Modules.Commercial.Infrastructure.Persistence.Configurations;

public sealed class InvoiceAttachmentConfiguration : IEntityTypeConfiguration<InvoiceAttachment>
{
    public void Configure(EntityTypeBuilder<InvoiceAttachment> builder)
    {
        builder.HasIndex(a => new { a.InvoiceId, a.Category }).HasDatabaseName("IX_InvoiceAttachment_Invoice_Category");

        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.StoredPath).HasMaxLength(512).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.UploadedByUserId).HasMaxLength(450);
        builder.Property(a => a.Description).HasMaxLength(300);

        builder.HasOne(a => a.Invoice)
            .WithMany()
            .HasForeignKey(a => a.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}