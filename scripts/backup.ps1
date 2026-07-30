<#
.SYNOPSIS
    Automated Backup Script for Rhyno ERP
.DESCRIPTION
    پشتیبان‌گیری خودکار از دیتابیس SQL Server، فایل‌های آپلود شده و تنظیمات سیستم
.PARAMETER BackupType
    نوع بکاپ: Full, Differential, or Log (پیش‌فرض: Full)
.PARAMETER RetentionDays
    تعداد روزهای نگهداری فایل‌های بکاپ (پیش‌فرض: 30)
.PARAMETER CompressBackups
    فشرده‌سازی فایل‌های بکاپ بعد از ایجاد (پیش‌فرض: true)
.PARAMETER VerifyBackup
    بررسی صحت بکاپ بعد از ایجاد (پیش‌فرض: true)
.EXAMPLE
    .\backup.ps1 -BackupType Full
    .\backup.ps1 -BackupType Differential -RetentionDays 7
#>

param(
    [ValidateSet("Full", "Differential", "Log")]
    [string]$BackupType = "Full",

    [int]$RetentionDays = 30,

    [bool]$CompressBackups = $true,

    [bool]$VerifyBackup = $true
)

# ── Configuration ──────────────────────────────────────
$BackupRoot = Join-Path $PSScriptRoot "..\backups"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ServerName = $env:COMPUTERNAME
$SqlServer = $env:SQL_SERVER ?? "localhost"
$SqlDatabase = $env:SQL_DATABASE ?? "RhynoPlatformDb"
$SqlUser = $env:SQL_USER ?? "sa"
$SqlPassword = $env:SQL_PASSWORD ?? "Rhyno@2024Secure!"
$UploadsPath = $env:UPLOADS_PATH ?? (Join-Path $PSScriptRoot "..\wwwroot\uploads")
$AppSettingsPath = Join-Path $PSScriptRoot "..\appsettings.json"
$LogFile = Join-Path $BackupRoot "backup-log-$Timestamp.txt"

# Create backup directories
$DatabaseBackupDir = Join-Path $BackupRoot "Database\$BackupType"
$FilesBackupDir = Join-Path $BackupRoot "Files"
$ConfigBackupDir = Join-Path $BackupRoot "Config"

foreach ($dir in @($DatabaseBackupDir, $FilesBackupDir, $ConfigBackupDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -Path $dir -ItemType Directory -Force | Out-Null
    }
}

# ── Logging Function ───────────────────────────────────
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $logMessage = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogFile -Value $logMessage
}

Write-Log "═══════════════════════════════════════════════════"
Write-Log "Rhyno ERP Backup Started - Type: $BackupType"
Write-Log "═══════════════════════════════════════════════════"

$backupSuccess = $true

try {
    # ── 1. Database Backup ─────────────────────────────
    Write-Log "Step 1/4: Database Backup ($BackupType)..."
    
    $dbBackupFile = Join-Path $DatabaseBackupDir "Rhyno_$BackupType-$Timestamp.bak"

    try {
        if ($BackupType -eq "Full") {
            $sqlCmd = @"
BACKUP DATABASE [$SqlDatabase]
TO DISK = N'$dbBackupFile'
WITH FORMAT, INIT, NAME = N'Rhyno ERP - $BackupType Backup',
SKIP, NOREWIND, NOUNLOAD, COMPRESSION, STATS = 10;
"@
        } elseif ($BackupType -eq "Differential") {
            $sqlCmd = @"
BACKUP DATABASE [$SqlDatabase]
TO DISK = N'$dbBackupFile'
WITH DIFFERENTIAL, NAME = N'Rhyno ERP - Differential Backup',
SKIP, NOREWIND, NOUNLOAD, COMPRESSION, STATS = 10;
"@
        } else {
            $sqlCmd = @"
BACKUP LOG [$SqlDatabase]
TO DISK = N'$dbBackupFile'
WITH NAME = N'Rhyno ERP - Log Backup',
SKIP, NOREWIND, NOUNLOAD, COMPRESSION, STATS = 10;
"@
        }

        sqlcmd -S $SqlServer -U $SqlUser -P $SqlPassword -Q $sqlCmd -b 2>&1 | Out-Null

        if ($LASTEXITCODE -eq 0) {
            $fileSize = (Get-Item $dbBackupFile).Length
            Write-Log "Database backup created: $dbBackupFile ($([math]::Round($fileSize / 1MB, 2)) MB)" "SUCCESS"
        } else {
            throw "sqlcmd failed with exit code $LASTEXITCODE"
        }
    }
    catch {
        Write-Log "Database backup FAILED: $_" "ERROR"
        $backupSuccess = $false
    }

    # ── 2. Verify Backup ───────────────────────────────
    if ($VerifyBackup -and (Test-Path $dbBackupFile)) {
        Write-Log "Step 2/4: Verifying backup..."
        try {
            $verifyCmd = @"
RESTORE VERIFYONLY
FROM DISK = N'$dbBackupFile'
WITH CHECKSUM;
"@
            sqlcmd -S $SqlServer -U $SqlUser -P $SqlPassword -Q $verifyCmd -b 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Log "Backup verification PASSED." "SUCCESS"
            } else {
                Write-Log "Backup verification WARNING - file may be corrupt!" "WARNING"
            }
        }
        catch {
            Write-Log "Backup verification FAILED: $_" "ERROR"
        }
    }

    # ── 3. Uploads Backup ──────────────────────────────
    if (Test-Path $UploadsPath) {
        Write-Log "Step 3/4: Backing up uploaded files..."
        try {
            $filesArchive = Join-Path $FilesBackupDir "uploads-$Timestamp.zip"
            
            if ($CompressBackups) {
                Compress-Archive -Path $UploadsPath -DestinationPath $filesArchive -Force
                $archiveSize = (Get-Item $filesArchive).Length
                Write-Log "Uploads backup created: $filesArchive ($([math]::Round($archiveSize / 1MB, 2)) MB)" "SUCCESS"
            } else {
                $copyDest = Join-Path $FilesBackupDir "uploads-$Timestamp"
                Copy-Item -Path $UploadsPath -Destination $copyDest -Recurse -Force
                Write-Log "Uploads copied to: $copyDest" "SUCCESS"
            }
        }
        catch {
            Write-Log "Uploads backup FAILED: $_" "WARNING"
        }
    }

    # ── 4. Configuration Backup ────────────────────────
    Write-Log "Step 4/4: Backing up configuration files..."
    try {
        if (Test-Path $AppSettingsPath) {
            $configDest = Join-Path $ConfigBackupDir "appsettings-$Timestamp.json"
            Copy-Item -Path $AppSettingsPath -Destination $configDest -Force
            Write-Log "Configuration backup created: $configDest" "SUCCESS"
        }
    }
    catch {
        Write-Log "Configuration backup FAILED: $_" "WARNING"
    }

    # ── 5. Cleanup Old Backups ─────────────────────────
    Write-Log "Cleaning up backups older than $RetentionDays days..."
    try {
        $oldFiles = Get-ChildItem -Path $BackupRoot -Recurse -File |
            Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) }
        
        $deletedCount = 0
        foreach ($file in $oldFiles) {
            Remove-Item $file.FullName -Force
            $deletedCount++
        }
        Write-Log "Removed $deletedCount old backup files." "INFO"

        # Also remove old log files
        $oldLogs = Get-ChildItem -Path $BackupRoot -Filter "backup-log-*.txt" -File |
            Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) }
        foreach ($log in $oldLogs) {
            Remove-Item $log.FullName -Force
        }
    }
    catch {
        Write-Log "Cleanup FAILED: $_" "WARNING"
    }

}
catch {
    Write-Log "Unexpected error: $_" "FATAL"
    $backupSuccess = $false
}
finally {
    Write-Log "═══════════════════════════════════════════════════"
    if ($backupSuccess) {
        Write-Log "BACKUP COMPLETED SUCCESSFULLY" "SUCCESS"
    } else {
        Write-Log "BACKUP COMPLETED WITH ERRORS - Check log for details" "WARNING"
    }
    Write-Log "Log file: $LogFile"
    Write-Log "═══════════════════════════════════════════════════"
}