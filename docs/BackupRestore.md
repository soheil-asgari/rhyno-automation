# Backup & Restore

## Backup

Back up the SQL Server database regularly using your standard DB tooling.

Recommended minimum policy:

- Daily full backup
- Hourly log backup if available
- Keep at least 7 daily and 4 weekly restore points

## Restore

1. Stop the application.
2. Restore the database backup.
3. Verify connection string.
4. Start the application.
5. Confirm `/health` returns healthy.

## Validation

After restore, confirm:

- Login works
- Audit log writes work
- Core roles exist
- Warehouse and financial dashboards load
