using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class ContractUserSettingConfiguration : IEntityTypeConfiguration<ContractUserSetting>
{
    public void Configure(EntityTypeBuilder<ContractUserSetting> builder)
    {
        builder.ToTable("ContractUserSettings");

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => item.UserId).IsUnique();

        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.DefaultStatus).HasMaxLength(40).IsRequired();

        builder.HasOne(item => item.User)
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.DefaultResponsibleUser)
            .WithMany()
            .HasForeignKey(item => item.DefaultResponsibleUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}