using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Platform.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
            builder
                            .HasIndex(setting => setting.ApplicationTitle);

            // These properties are stored dynamically in TenantSettings as key-value pairs,
            // so we ignore them on the SystemSettings database table mapping.
            builder.Ignore(setting => setting.PrimaryColor);
            builder.Ignore(setting => setting.AccentColor);
            builder.Ignore(setting => setting.LogoUrl);
            builder.Ignore(setting => setting.FaviconUrl);
            builder.Ignore(setting => setting.LoginBackgroundUrl);
            builder.Ignore(setting => setting.DesktopBackgroundUrl);
    }
}
