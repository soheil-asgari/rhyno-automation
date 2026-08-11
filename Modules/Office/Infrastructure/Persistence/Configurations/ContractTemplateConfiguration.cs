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

        builder.HasIndex(item => item.UserId);
        builder.HasIndex(item => new { item.UserId, item.IsDefault });

        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(500);

        builder.HasOne(item => item.User)
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}