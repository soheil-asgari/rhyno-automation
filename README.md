# Rhyno Automation

پنل اتوماسیون اداری و سازمانی مبتنی بر ASP.NET Core.

## شروع سریع

1. پیش‌نیازها:
   - .NET SDK 10
   - SQL Server یا LocalDB
   - دسترسی به `appsettings.Development.json` یا User Secrets برای تنظیم secrets

2. تنظیمات محرمانه:
   - `ConnectionStrings:DefaultConnection`
   - `BootstrapAdmin:Email`
   - `BootstrapAdmin:Password`
   - `OpenAI:ApiKey`

3. اجرا:
   ```bash
   dotnet restore
   dotnet build
   dotnet run
   ```

4. سلامت سرویس:
   - `GET /health`

## مستندات

- [Installation Guide](docs/Installation.md)
- [Admin Guide](docs/AdminGuide.md)
- [User Guide](docs/UserGuide.md)
- [Migration & Roles](docs/MigrationAndRoles.md)
- [Backup & Restore](docs/BackupRestore.md)
- [Deployment Guide](docs/Deployment.md)
