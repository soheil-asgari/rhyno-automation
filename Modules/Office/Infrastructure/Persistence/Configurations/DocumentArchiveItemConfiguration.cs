using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class DocumentArchiveItemConfiguration : IEntityTypeConfiguration<DocumentArchiveItem>
{
    public void Configure(EntityTypeBuilder<DocumentArchiveItem> builder)
    {
            builder
                            .Property(item => item.Title)
                            .HasMaxLength(180);

            builder
                            .Property(item => item.Category)
                            .HasMaxLength(40);

            builder
                            .Property(item => item.AccessLevel)
                            .HasMaxLength(40);

            builder
                            .Property(item => item.RelatedModule)
                            .HasMaxLength(120);

            builder
                            .Property(item => item.RelatedEntityType)
                            .HasMaxLength(120);

            builder
                            .Property(item => item.FileName)
                            .HasMaxLength(260);

            builder
                            .Property(item => item.StoredFileName)
                            .HasMaxLength(260);

            builder
                            .Property(item => item.RelativePath)
                            .HasMaxLength(400);

            builder
                            .Property(item => item.ContentType)
                            .HasMaxLength(120);

            builder
                  .Property(item => item.CreatedByUserId)
                  .HasMaxLength(450)
                  .IsRequired();

            builder
                            .HasIndex(item => new { item.Category, item.CreatedAt });

            builder
                            .HasOne(item => item.CreatedByUser)
                            .WithMany()
                            .HasForeignKey(item => item.CreatedByUserId)
                            .OnDelete(DeleteBehavior.Restrict);
    }
}
