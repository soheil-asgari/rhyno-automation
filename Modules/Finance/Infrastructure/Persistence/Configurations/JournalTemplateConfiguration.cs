using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class JournalTemplateConfiguration : IEntityTypeConfiguration<JournalTemplate>
{
    public void Configure(EntityTypeBuilder<JournalTemplate> builder)
    {
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasMaxLength(600);

        builder.Property(item => item.JournalTypeId)
            .IsRequired();

        builder.Property(item => item.RecurrenceEveryMonths);

        builder.Property(item => item.NextRunDate);

        builder.Property(item => item.IsRecurring)
            .HasDefaultValue(false);

        builder.Property(item => item.IsActive)
            .HasDefaultValue(true);

        builder.Property(item => item.Reference)
            .HasMaxLength(120);

        builder.HasIndex(item => item.Name)
            .IsUnique();

        builder.HasOne(item => item.JournalType)
            .WithMany()
            .HasForeignKey(item => item.JournalTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.Lines)
            .WithOne(item => item.JournalTemplate)
            .HasForeignKey(item => item.JournalTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class JournalTemplateLineConfiguration : IEntityTypeConfiguration<JournalTemplateLine>
{
    public void Configure(EntityTypeBuilder<JournalTemplateLine> builder)
    {
        builder.HasKey(item => item.Id);

        builder.Property(item => item.JournalTemplateId)
            .IsRequired();

        builder.Property(item => item.SubsidiaryAccountId)
            .IsRequired();

        builder.Property(item => item.DebitAmount)
            .HasPrecision(18, 2);

        builder.Property(item => item.CreditAmount)
            .HasPrecision(18, 2);

        builder.Property(item => item.Narration)
            .HasMaxLength(600);

        builder.Property(item => item.DisplayOrder);

        builder.HasOne(item => item.SubsidiaryAccount)
            .WithMany()
            .HasForeignKey(item => item.SubsidiaryAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.FloatingDetailAccount)
            .WithMany()
            .HasForeignKey(item => item.FloatingDetailAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}