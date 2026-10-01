using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OfficeAutomation.Services.Licensing;

public sealed class LicenseContext : ILicenseContext
{
    public const string DefaultLicenseRelativePath = "App_Data/license/current.lic";
    public const string DefaultHeartbeatStateRelativePath = "App_Data/license/heartbeat-state.json";

    private readonly ILicenseValidator _licenseValidator;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptions<LicensingOptions>? _options;
    private readonly ILogger<LicenseContext> _logger;
    private readonly object _lock = new();

    private LicenseValidationResult _currentResult;
    private string? _customLicensePath;
    private HeartbeatState _heartbeatState = new();

    public LicenseContext(
        ILicenseValidator licenseValidator,
        IHostEnvironment hostEnvironment,
        ILogger<LicenseContext> logger,
        IOptions<LicensingOptions>? options = null)
    {
        _licenseValidator = licenseValidator;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
        _options = options;
        _currentResult = LicenseValidationResult.Failure(
            LicenseValidationFailureReason.Missing,
            "لایسنس معتبری یافت نشد — لطفاً نسبت به بارگذاری و فعال‌سازی لایسنس اقدام نمایید.");

        LoadHeartbeatState();
    }

    public LicenseInfo? CurrentLicense => _currentResult.License;

    public LicenseValidationResult ValidationResult => _currentResult;

    public bool IsLicensed
    {
        get
        {
            if (!_currentResult.IsValid || _currentResult.License == null)
            {
                return false;
            }

            // If online check is required, evaluate heartbeat grace period
            if (_currentResult.License.OnlineCheckRequired && GraceStatus == GracePeriodStatus.Expired)
            {
                return false;
            }

            return true;
        }
    }

    public bool IsLocked => !IsLicensed;

    public string? LockReason
    {
        get
        {
            if (IsLicensed) return null;

            if (_currentResult.IsValid && _currentResult.License != null && _currentResult.License.OnlineCheckRequired && GraceStatus == GracePeriodStatus.Expired)
            {
                var graceDays = _options?.Value?.HeartbeatGraceDays ?? 21;
                return $"مهلت اتصال به سرور لایسنس ({graceDays} روز بدون ارتباط موفق) به پایان رسیده است و سامانه قفل شده است. لطفاً اتصال اینترنت و سرور لایسنس را بررسی نمایید.";
            }

            return _currentResult.FailureReason switch
            {
                LicenseValidationFailureReason.Expired when _currentResult.License?.ExpiresAtUtc != null =>
                    FormatExpiredPersianMessage(_currentResult.License.ExpiresAtUtc.Value),
                LicenseValidationFailureReason.Expired =>
                    "اعتبار زمانی لایسنس به پایان رسیده است — لطفاً نسبت به تمدید لایسنس اقدام نمایید.",
                LicenseValidationFailureReason.FingerprintMismatch =>
                    "مشخصات سخت‌افزاری سرور فعلی با اثر انگشت لایسنس مطابقت ندارد (Fingerprint Mismatch).",
                LicenseValidationFailureReason.InvalidSignature =>
                    "امضای دیجیتال فایل لایسنس نامعتبر است یا فایل دستکاری شده است.",
                LicenseValidationFailureReason.Malformed =>
                    "فایل لایسنس مخدوش یا نامعتبر است.",
                LicenseValidationFailureReason.Revoked =>
                    "لایسنس نرم‌افزار توسط شرکت ارائه‌دهنده ابطال شده است (Revoked) — لطفاً با پشتیبانی تماس بگیرید.",
                LicenseValidationFailureReason.Missing =>
                    "هیچ لایسنس معتبری برای سامانه ثبت نشده است — لطفاً فایل لایسنس را بارگذاری نمایید.",
                _ => _currentResult.ErrorMessage ?? "لایسنس سامانه نامعتبر است."
            };
        }
    }

    public DateTime? LastSuccessfulHeartbeatAt
    {
        get
        {
            lock (_lock)
            {
                return _heartbeatState.LastSuccessfulHeartbeatAt;
            }
        }
    }

    public int? GraceDaysRemaining
    {
        get
        {
            var lic = _currentResult.License;
            if (lic == null || !lic.OnlineCheckRequired)
            {
                return null;
            }

            var graceDays = _options?.Value?.HeartbeatGraceDays ?? 21;
            if (graceDays <= 0) graceDays = 21;

            DateTime baselineUtc;
            lock (_lock)
            {
                if (_heartbeatState.LastSuccessfulHeartbeatAt.HasValue)
                {
                    baselineUtc = _heartbeatState.LastSuccessfulHeartbeatAt.Value;
                }
                else if (lic.IssuedAtUtc != default)
                {
                    baselineUtc = lic.IssuedAtUtc;
                }
                else if (_heartbeatState.FirstSeenAtUtc.HasValue)
                {
                    baselineUtc = _heartbeatState.FirstSeenAtUtc.Value;
                }
                else
                {
                    baselineUtc = DateTime.UtcNow;
                }
            }

            var deadline = baselineUtc.AddDays(graceDays);
            var remaining = (deadline - DateTime.UtcNow).TotalDays;
            return remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
        }
    }

    public GracePeriodStatus GraceStatus
    {
        get
        {
            var lic = _currentResult.License;
            if (lic == null || !lic.OnlineCheckRequired)
            {
                return GracePeriodStatus.NotApplicable;
            }

            var days = GraceDaysRemaining;
            if (!days.HasValue)
            {
                return GracePeriodStatus.NotApplicable;
            }

            if (days.Value <= 0)
            {
                return GracePeriodStatus.Expired;
            }

            if (days.Value <= 2)
            {
                return GracePeriodStatus.WarningRed;
            }

            if (days.Value <= 7)
            {
                return GracePeriodStatus.WarningAmber;
            }

            return GracePeriodStatus.Normal;
        }
    }

    public string? GraceWarningBanner
    {
        get
        {
            var status = GraceStatus;
            var days = GraceDaysRemaining;
            if (!days.HasValue) return null;

            if (status == GracePeriodStatus.WarningRed)
            {
                return $"هشدار فوری: ارتباط با سرور لایسنس برقرار نشده است و تنها {days.Value} روز تا قفل شدن کامل سامانه باقی مانده است. لطفاً وضعیت اتصال سرور را بررسی نمایید.";
            }

            if (status == GracePeriodStatus.WarningAmber)
            {
                return $"هشدار اتصال لایسنس: ارتباط با سرور لایسنس بیش از حد معمول به طول انجامیده است. {days.Value} روز تا اتمام مهلت اتصال (Grace Period) باقی مانده است.";
            }

            return null;
        }
    }

    public bool IsModuleEnabled(string moduleName)
    {
        if (IsLocked || CurrentLicense == null)
        {
            return false;
        }

        return CurrentLicense.HasModule(moduleName);
    }

    public async Task<LicenseValidationResult> RefreshAsync(string? licenseFilePath = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(licenseFilePath))
        {
            _customLicensePath = licenseFilePath;
        }

        var fullPath = GetLicenseFullPath(_customLicensePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("License file not found at path: {Path}. Application running in Locked mode.", fullPath);
            lock (_lock)
            {
                _currentResult = LicenseValidationResult.Failure(
                    LicenseValidationFailureReason.Missing,
                    "فایل لایسنس یافت نشد — لطفاً لایسنس را بارگذاری و فعال نمایید.");
            }
            return _currentResult;
        }

        try
        {
            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            var result = await _licenseValidator.ValidateAsync(content, cancellationToken);

            lock (_lock)
            {
                _currentResult = result;
                if (result.IsValid && result.License != null)
                {
                    EnsureFirstSeenState(result.License.LicenseId);
                }
            }

            if (result.IsValid && result.License != null)
            {
                _logger.LogInformation(
                    "License validated successfully for customer '{Customer}'. Type: {Type}, Modules: {Modules}, MaxUsers: {MaxUsers}, OnlineCheckRequired: {OnlineCheck}",
                    result.License.CustomerName,
                    result.License.LicenseType,
                    string.Join(", ", result.License.Modules),
                    result.License.MaxUsers,
                    result.License.OnlineCheckRequired);
            }
            else
            {
                _logger.LogWarning(
                    "License at {Path} is invalid: Reason: {Reason}, Error: {Error}. Application operating in Locked mode.",
                    fullPath,
                    result.FailureReason,
                    result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read or validate license file at {Path}.", fullPath);
            lock (_lock)
            {
                _currentResult = LicenseValidationResult.Failure(
                    LicenseValidationFailureReason.Malformed,
                    $"خطا در خواندن فایل لایسنس: {ex.Message}");
            }
            return _currentResult;
        }
    }

    public async Task<LicenseValidationResult> ActivateLicenseAsync(string licenseFileContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseFileContent))
        {
            return LicenseValidationResult.Failure(LicenseValidationFailureReason.Missing, "محتوای لایسنس خالی است.");
        }

        // Validate first before saving to disk
        var result = await _licenseValidator.ValidateAsync(licenseFileContent, cancellationToken);
        if (!result.IsValid)
        {
            _logger.LogWarning("Attempted activation of invalid license: {Reason} - {Error}", result.FailureReason, result.ErrorMessage);
            return result;
        }

        var fullPath = GetLicenseFullPath(_customLicensePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(fullPath, licenseFileContent.Trim(), cancellationToken);

        lock (_lock)
        {
            _currentResult = result;
            if (result.License != null)
            {
                EnsureFirstSeenState(result.License.LicenseId);
            }
        }

        _logger.LogInformation("New license activated and saved to {Path} successfully.", fullPath);
        return result;
    }

    public void MarkRevoked(string reason)
    {
        lock (_lock)
        {
            _currentResult = LicenseValidationResult.Failure(
                LicenseValidationFailureReason.Revoked,
                string.IsNullOrWhiteSpace(reason)
                    ? "لایسنس نرم‌افزار توسط شرکت ارائه‌دهنده ابطال شده است (Revoked) — لطفاً با پشتیبانی تماس بگیرید."
                    : reason);

            _heartbeatState.LastStatus = "revoked";
            _heartbeatState.LastErrorMessage = reason;
            SaveHeartbeatState();
        }

        _logger.LogCritical("License marked as REVOKED. System moved into Locked mode. Reason: {Reason}", reason);
    }

    public async Task UpdateHeartbeatStateAsync(Action<HeartbeatState> updateAction, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            updateAction(_heartbeatState);
            SaveHeartbeatState();
        }

        await Task.CompletedTask;
    }

    private void EnsureFirstSeenState(Guid licenseId)
    {
        if (!_heartbeatState.FirstSeenAtUtc.HasValue || _heartbeatState.LastLicenseId != licenseId)
        {
            _heartbeatState.FirstSeenAtUtc = DateTime.UtcNow;
            _heartbeatState.LastLicenseId = licenseId;
            SaveHeartbeatState();
        }
    }

    private void LoadHeartbeatState()
    {
        lock (_lock)
        {
            try
            {
                var fullPath = Path.Combine(_hostEnvironment.ContentRootPath, DefaultHeartbeatStateRelativePath);
                if (File.Exists(fullPath))
                {
                    var json = File.ReadAllText(fullPath);
                    var state = JsonSerializer.Deserialize<HeartbeatState>(json);
                    if (state != null)
                    {
                        _heartbeatState = state;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load heartbeat state from disk. Initializing new state.");
            }

            _heartbeatState = new HeartbeatState();
        }
    }

    private void SaveHeartbeatState()
    {
        try
        {
            var fullPath = Path.Combine(_hostEnvironment.ContentRootPath, DefaultHeartbeatStateRelativePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_heartbeatState, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(fullPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist heartbeat state to disk.");
        }
    }

    private string GetLicenseFullPath(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            return Path.IsPathRooted(customPath)
                ? customPath
                : Path.Combine(_hostEnvironment.ContentRootPath, customPath);
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, DefaultLicenseRelativePath);
    }

    private static string FormatExpiredPersianMessage(DateTime expiredUtc)
    {
        try
        {
            var pc = new PersianCalendar();
            var localTime = expiredUtc.ToLocalTime();
            var year = pc.GetYear(localTime);
            var month = pc.GetMonth(localTime).ToString("00");
            var day = pc.GetDayOfMonth(localTime).ToString("00");
            return $"لایسنس نرم‌افزار در تاریخ {year}/{month}/{day} منقضی شده است — لطفاً نسبت به تمدید اقدام نمایید.";
        }
        catch
        {
            return $"لایسنس نرم‌افزار در تاریخ {expiredUtc:yyyy-MM-dd} UTC منقضی شده است — لطفاً نسبت به تمدید اقدام نمایید.";
        }
    }
}
