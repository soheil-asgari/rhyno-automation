using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
            builder
                            .HasIndex(preference => preference.UserId)
                            .IsUnique();

            builder
                            .Property(preference => preference.TablePreferencesJson)
                            .HasMaxLength(8000);

            builder
                            .HasOne(preference => preference.User)
                            .WithMany()
                            .HasForeignKey(preference => preference.UserId)
                            .OnDelete(DeleteBehavior.Cascade);
    }
}
