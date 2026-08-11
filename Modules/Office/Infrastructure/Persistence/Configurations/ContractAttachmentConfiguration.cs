using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class ContractAttachmentConfiguration : IEntityTypeConfiguration<ContractAttachment>
{
    public void Configure(EntityTypeBuilder<ContractAttachment> builder)
    {
        builder.ToTable("ContractAttachments");

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => item.ContractId);

        builder.Property(item => item.FileName).HasMaxLength(260).IsRequired();
        builder.Property(item => item.StoredFileName).HasMaxLength(260).IsRequired();
        builder.Property(item => item.RelativePath).HasMaxLength(400).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(120);
        builder.Property(item => item.UploadedByUserId).HasMaxLength(450).IsRequired();
    }
}