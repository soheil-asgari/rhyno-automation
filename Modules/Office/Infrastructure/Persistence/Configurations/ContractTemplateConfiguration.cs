using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class ContractTemplateConfiguration : IEntityTypeConfiguration<ContractTemplate>
{
    public void Configure(EntityTypeBuilder<ContractTemplate> builder)
    {
        builder.ToTable("ContractTemplates");

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => item.OwnerUserId);
        builder.HasIndex(item => new { item.OwnerUserId, item.IsDefault });

        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.OwnerUserId).HasMaxLength(450);
        builder.Property(item => item.Description).HasMaxLength(500);
        builder.Property(item => item.TemplateType).HasMaxLength(20);

        builder.HasOne(item => item.OwnerUser)
            .WithMany()
            .HasForeignKey(item => item.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}