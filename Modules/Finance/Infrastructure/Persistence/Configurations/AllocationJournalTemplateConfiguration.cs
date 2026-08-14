using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class AllocationJournalTemplateConfiguration : IEntityTypeConfiguration<AllocationJournalTemplate>
{
    public void Configure(EntityTypeBuilder<AllocationJournalTemplate> builder)
    {
        builder.ToTable("AllocationJournalTemplate");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");

        builder.HasOne(e => e.SourceSubsidiaryAccount)
            .WithMany()
            .HasForeignKey(e => e.SourceSubsidiaryAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.JournalType)
            .WithMany()
            .HasForeignKey(e => e.JournalTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AllocationJournalTemplateLineConfiguration : IEntityTypeConfiguration<AllocationJournalTemplateLine>
{
    public void Configure(EntityTypeBuilder<AllocationJournalTemplateLine> builder)
    {
        builder.ToTable("AllocationJournalTemplateLine");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Percentage).HasColumnType("decimal(7,4)");
        builder.Property(e => e.Ratio).HasColumnType("decimal(18,6)");
        builder.Property(e => e.Narration).HasMaxLength(500);

        builder.HasOne(e => e.AllocationJournalTemplate)
            .WithMany(t => t.Lines)
            .HasForeignKey(e => e.AllocationJournalTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.TargetSubsidiaryAccount)
            .WithMany()
            .HasForeignKey(e => e.TargetSubsidiaryAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}