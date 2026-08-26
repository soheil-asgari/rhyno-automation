# reset-dev-db.ps1
# Drops the desynced development database(s) so EF Core can recreate them
# cleanly from the current (squashed) migrations on next app startup.
#
# WHY: The OfficeDbContext migrations were squashed into a single
# "InitialOfficeDbCreate" migration while the dev database still contains
# the OLD incremental migration history plus the already-created tables.
# This causes EF Core to try "CREATE TABLE [AspNetUsers]" against a table
# that already exists -> error 2714.
#
# Safe to run: it only drops DEVELOPMENT databases and the app re-seeds
# the admin user via BootstrapAdmin on startup.

param(
    [string[]]$Databases = @("OfficeAutoDbDev", "OfficeAutoDb")
)

# LocalDB instance used by the connection strings in appsettings*.json
$Server = "(localdb)\mssqllocaldb"

# Use the in-box System.Data.SqlClient (available in Windows PowerShell 5.1)
Add-Type -AssemblyName System.Data

foreach ($db in $Databases) {
    $conn = New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=master;Trusted_Connection=True;Connect Timeout=15"
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()

        # Check whether the database exists
        $cmd.CommandText = "SELECT DB_ID('$db')"
        $dbId = $cmd.ExecuteScalar()

        if ($null -eq $dbId -or $dbId -is [DBNull]) {
            Write-Host "[SKIP] Database '$db' does not exist." -ForegroundColor DarkGray
        }
        else {
            # Force-disconnect any sessions and drop
            $cmd.CommandText = "ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE"
            $cmd.ExecuteNonQuery() | Out-Null

            $cmd.CommandText = "DROP DATABASE [$db]"
            $cmd.ExecuteNonQuery() | Out-Null

            Write-Host "[OK] Dropped database '$db'." -ForegroundColor Green
        }
    }
    catch {
        Write-Host "[ERROR] Failed to drop '$db': $($_.Exception.Message)" -ForegroundColor Red
    }
    finally {
        if ($conn.State -eq 'Open') { $conn.Close() }
        $conn.Dispose()
    }
}

Write-Host ""
Write-Host "Done. Run the app now; EF Core will recreate the schema from migrations." -ForegroundColor Cyan
