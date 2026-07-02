using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class SubsidiaryAccountFloatingDetailConfiguration : IEntityTypeConfiguration<SubsidiaryAccountFloatingDetail>
{
    public void Configure(EntityTypeBuilder<SubsidiaryAccountFloatingDetail> builder)
    {
        builder.HasKey(item => new { item.SubsidiaryAccountId, item.FloatingDetailAccountId });

        builder.HasOne(item => item.SubsidiaryAccount)
            .WithMany(item => item.FloatingDetailLinks)
            .HasForeignKey(item => item.SubsidiaryAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.FloatingDetailAccount)
            .WithMany(item => item.SubsidiaryAccountLinks)
            .HasForeignKey(item => item.FloatingDetailAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
