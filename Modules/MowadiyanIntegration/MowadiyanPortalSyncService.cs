using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeAutomation.Modules.InvoiceStructuring;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>Stage of the Mowadiyan output-file (درخواست فایل خروجی) export pipeline.</summary>
public enum MowadiyanExportStage
{
    Idle = 0,
    Authenticating = 1,
    Submitting = 2,
    Producing = 3,
    Downloading = 4,
    Importing = 5,
    Completed = 6,
    Failed = 7
}

/// <summary>
/// Parameters of an output-file export request, mirroring the "درخواست فایل خروجی" modal:
/// export/transaction type fixed to purchase invoices with all statuses &lt;-&gt; patterns.
/// </summary>
public sealed class MowadiyanPortalExportRequest
{
    /// <summary>Selected tax year (سال مالیاتی), e.g. 1405.</summary>
    public int TaxYear { get; set; }

    /// <summary>Selected tax period / season (دوره مالیاتی): 1..4.</summary>
    public int TaxPeriod { get; set; }

    /// <summary>Manual SMS OTP code entered by the user to complete the session.</summary>
    public string OtpCode { get; set; } = string.Empty;

    /// <summary>Fixed export type for "خروجی از صورتحساب‌ها".</summary>
    public string ExportType { get; set; } = "INVOICE";

    /// <summary>Fixed transaction type for domestic purchases (خرید داخلی).</summary>
    public string TransactionType { get; set; } = "BUY_DOMESTIC";

    /// <summary>All invoice statuses (تمامی وضعیت‌ها).</summary>
    public string InvoiceStatuses { get; set; } = "ALL";

    /// <summary>All invoice patterns (تمامی الگوها).</summary>
    public string InvoicePatterns { get; set; } = "ALL";

    /// <summary>
    /// True when the portal session is already active so no SMS OTP is needed
    /// (surfaced by <c>initiate</c> as the <c>ALREADY_LOGGED_IN</c> status).
    /// When true, <see cref="OtpCode"/> may be empty.
    /// </summary>
    public bool AlreadyLoggedIn { get; set; }
}

/// <summary>Realtime progress of an export job, returned to the UI for polling.</summary>
public sealed class MowadiyanPortalExportProgress
{
    public Guid JobId { get; set; }
    public MowadiyanExportStage Stage { get; set; }
    public string StageText { get; set; } = "Idle";
    public string Message { get; set; } = string.Empty;
    public int PollElapsedSeconds { get; set; }
    public int? TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Error { get; set; }
    public bool IsComplete { get; set; }
    public bool IsFailed { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Result of initiating the portal login challenge (which triggers the SMS OTP).</summary>
public sealed class MowadiyanPortalInitiationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ChallengeToken { get; set; }
    public bool IsSandbox { get; set; }
    public string? MaskedPhone { get; set; }

    /// <summary>Raw status returned by the Python exporter (e.g. "ALREADY_LOGGED_IN").</summary>
    public string? Status { get; set; }

    /// <summary>True when the portal session was already active (no OTP needed).</summary>
    public bool AlreadyLoggedIn { get; set; }
}

/// <inheritdoc cref="MowadiyanPortalSyncService" />
public interface IMowadiyanPortalSyncService
{
    /// <summary>
    /// Starts the background export/import job using the saved portal credentials and the
    /// user-supplied SMS OTP code. Returns the job id used for progress polling.
    /// </summary>
    Task<Guid> StartExportAsync(MowadiyanPortalExportRequest request, CancellationToken cancellationToken);

    /// <summary>Returns the current progress of a job (or null when unknown / expired).</summary>
    MowadiyanPortalExportProgress? GetProgress(Guid jobId);

    /// <summary>Resolves the persisted Mowadiyan portal settings.</summary>
    Task<MowadiyanSettings?> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Initiates the portal login challenge: runs the Python exporter with <c>--action initiate</c>,
    /// which submits the saved portal username/password, solves the captcha and triggers an SMS OTP
    /// to the user's phone. Returns the active session/challenge token. No OTP is consumed here.
    /// </summary>
    Task<MowadiyanPortalInitiationResult> InitiatePortalLoginAsync(CancellationToken cancellationToken);
}
/// <summary>
/// Implements the Tax Portal export-request flow (https://tp.tax.gov.ir/invoice/outputFiles)
/// by delegating all browser/site interaction to the Python Playwright script
/// (<c>scripts/tax_exporter.py</c>) and orchestrating the database work in C#:
/// parse the downloaded .xlsx/.zip and upsert matched rows into
/// <see cref="TaxDbContext.InvoiceRecords"/> (InvoiceType = Purchase, IsMowadiyanSynced = true).
/// Progress is held in-memory and consumed by <see cref="Controllers.MowadiyanController"/>.
/// </summary>
public sealed class MowadiyanPortalSyncService : IMowadiyanPortalSyncService
{
    private const int MaxJobLifetimeMinutes = 30;

    /// <summary>Python <c>initiate</c> status meaning the session was already active.</summary>
    private const string AlreadyLoggedInStatus = "ALREADY_LOGGED_IN";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MowadiyanOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<MowadiyanPortalSyncService> _logger;
    private readonly ConcurrentDictionary<Guid, MowadiyanPortalExportProgress> _jobs = new();

    public MowadiyanPortalSyncService(
        IServiceScopeFactory scopeFactory,
        IOptions<MowadiyanOptions> options,
        IHostEnvironment env,
        ILogger<MowadiyanPortalSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _env = env;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> StartExportAsync(MowadiyanPortalExportRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.TaxYear < 1390 || request.TaxYear > 1500 || request.TaxPeriod < 1 || request.TaxPeriod > 4)
        {
            throw new ArgumentException("سال مالیاتی یا دوره انتخاب‌شده نامعتبر است.");
        }

        var settings = await GetSettingsAsync(cancellationToken);
        if (settings is null)
        {
            throw new InvalidOperationException("تنظیمات سامانه مودیان ثبت نشده است. ابتدا تنظیمات را ذخیره کنید.");
        }

        if (string.IsNullOrWhiteSpace(settings.PortalUsername))
        {
            throw new InvalidOperationException("نام کاربری پورتال سامانه مودیان در تنظیمات ثبت نشده است.");
        }

        if (!settings.IsSandbox && !request.AlreadyLoggedIn && string.IsNullOrWhiteSpace(request.OtpCode))
        {
            throw new InvalidOperationException("کد تأیید پیامکی (OTP) وارد نشده است.");
        }

        // Merge runtime option fallbacks into the persisted settings.
        if (string.IsNullOrWhiteSpace(settings.BaseUrl)) settings.BaseUrl = _options.Value.BaseUrl;
        settings.IsSandbox = _options.Value.IsSandbox || settings.IsSandbox;

        var jobId = Guid.NewGuid();
        var progress = new MowadiyanPortalExportProgress
        {
            JobId = jobId,
            Stage = MowadiyanExportStage.Idle,
            StageText = "Idle",
            Message = "در انتظار شروع...",
            StartedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _jobs[jobId] = progress;

        // Fire-and-forget background processing so the UI can poll progress.
        // The job deliberately does not inherit the incoming request's aborted token.
        _ = Task.Run(() => RunExportJobAsync(jobId, request, settings, CancellationToken.None), CancellationToken.None);
        return jobId;
    }

    /// <inheritdoc />
    public MowadiyanPortalExportProgress? GetProgress(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var snapshot))
        {
            // Expire long-dormant jobs from memory.
            if (snapshot.IsComplete && snapshot.UpdatedAt.HasValue
                && (DateTime.Now - snapshot.UpdatedAt.Value).TotalMinutes > MaxJobLifetimeMinutes)
            {
                _jobs.TryRemove(jobId, out _);
                return null;
            }
            return snapshot;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<MowadiyanSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TaxDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.MowadiyanSettings
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
/// <inheritdoc />
    public async Task<MowadiyanPortalInitiationResult> InitiatePortalLoginAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return Fail("تنظیمات سامانه مودیان ثبت نشده است. ابتدا تنظیمات را ذخیره کنید.");
        }

        if (string.IsNullOrWhiteSpace(settings.PortalUsername))
        {
            return Fail("نام کاربری پورتال سامانه مودیان در تنظیمات ثبت نشده است.");
        }

        if (string.IsNullOrWhiteSpace(settings.PortalPassword))
        {
            return Fail("رمز عبور پورتال سامانه مودیان در تنظیمات ثبت نشده است.");
        }

        if (settings.IsSandbox)
        {
            _logger.LogInformation("Sandbox Mowadiyan portal login challenge initiated (SMS OTP simulated).");
            return new MowadiyanPortalInitiationResult
            {
                Success = true,
                Message = "کد پیامک با موفقیت به شماره شما ارسال شد.",
                ChallengeToken = $"sandbox-challenge-{Guid.NewGuid():N}",
                IsSandbox = true
            };
        }

        // Real portal: delegate the login challenge (username/password + captcha + SMS OTP
        // trigger) to the Python Playwright script. The OTP itself arrives via SMS.
        var outDir = ResolveOutDir();
        var result = await RunTaxExporterAsync("initiate", settings, otp: null, request: null, outDir, cancellationToken);
        if (!result.Success)
        {
            return new MowadiyanPortalInitiationResult
            {
                Success = false,
                Message = result.Message,
                IsSandbox = false
            };
        }

        return new MowadiyanPortalInitiationResult
        {
            Success = true,
            Message = result.Message ?? "کد پیامک با موفقیت به شماره شما ارسال شد.",
            ChallengeToken = string.IsNullOrWhiteSpace(result.ChallengeToken)
                ? $"python-challenge-{Guid.NewGuid():N}"
                : result.ChallengeToken,
            IsSandbox = false,
            MaskedPhone = result.MaskedPhone,
            Status = result.Status,
            AlreadyLoggedIn = string.Equals(result.Status, AlreadyLoggedInStatus, StringComparison.Ordinal)
        };
    }
private async Task RunExportJobAsync(
        Guid jobId, MowadiyanPortalExportRequest request, MowadiyanSettings settings, CancellationToken ct)
    {
        var progress = _jobs[jobId];
        try
        {
            if (settings.IsSandbox)
            {
                await RunSandboxJobAsync(progress, request, ct);
                return;
            }

            // Step 1 — Run the Python exporter (`complete`) which authenticates with the SMS
            // OTP, requests the BUY_DOMESTIC output file and downloads it into --outdir.
            Update(progress, MowadiyanExportStage.Authenticating, "اجرای اسکریپت پایتون و احراز هویت با کد پیامکی...");
            var outDir = ResolveOutDir();
            var py = await RunTaxExporterAsync("complete", settings, request.OtpCode, request, outDir, ct);
            if (!py.Success)
            {
                throw new InvalidOperationException(py.Message ?? "اجرای اسکریپت پایتون ناموفق بود.");
            }

            if (string.IsNullOrWhiteSpace(py.FilePath) || !System.IO.File.Exists(py.FilePath))
            {
                throw new InvalidOperationException("مسیر فایل خروجی از اسکریپت پایتون دریافت نشد.");
            }

            // Step 2 — The file is already on disk; parse the workbook (or ZIP archive) into rows.
            Update(progress, MowadiyanExportStage.Downloading, "دریافت فایل خروجی از سامانه مودیان...");
            var bytes = await System.IO.File.ReadAllBytesAsync(py.FilePath, ct);
            var rows = ParseWorkbookRows(bytes);
            if (rows.Count == 0)
            {
                throw new InvalidOperationException("فایل خروجی دریافتی حاوی هیچ سطری نیست.");
            }

            // Step 3 — Upsert parsed purchase records into the database.
            Update(progress, MowadiyanExportStage.Importing, "ثبت خودکار فاکتورها در دیتابیس...");
            var imported = await UpsertRowsAsync(request, rows, ct);

            progress.TotalRows = imported;
            progress.ImportedRows = imported;
            progress.DownloadUrl = py.FilePath;
            Update(progress, MowadiyanExportStage.Completed,
                $"دانلود و ثبت {imported.ToString(CultureInfo.InvariantCulture)} فاکتور با موفقیت انجام شد.");

            _logger.LogInformation("Mowadiyan portal export job {JobId} completed; {Count} rows imported from {Path}.",
                jobId, imported, py.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mowadiyan portal export job {JobId} failed: {Message}", jobId, ex.Message);
            progress.Error = ex.Message;
            progress.IsFailed = true;
            progress.IsComplete = true;
            progress.Stage = MowadiyanExportStage.Failed;
            progress.StageText = "Failed";
            progress.Message = "عملیات ناموفق بود: " + ex.Message;
            progress.UpdatedAt = DateTime.Now;
        }
    }
/// <summary>Offline sandbox job so integration tests / demos work without a portal session.</summary>
    private async Task RunSandboxJobAsync(
        MowadiyanPortalExportProgress progress, MowadiyanPortalExportRequest request, CancellationToken ct)
    {
        await Task.Yield();
        Update(progress, MowadiyanExportStage.Authenticating, "احراز هویت (sandbox)...");
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        Update(progress, MowadiyanExportStage.Submitting,
            $"ارسال درخواست خروجی (خرید داخلی - تمام وضعیت‌ها) برای سال {request.TaxYear.ToString(CultureInfo.InvariantCulture)} دوره {request.TaxPeriod.ToString(CultureInfo.InvariantCulture)} (sandbox)...");
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        Update(progress, MowadiyanExportStage.Producing, "در حال تولید فایل توسط سامانه مودیان (sandbox)...");
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        Update(progress, MowadiyanExportStage.Downloading, "دریافت فایل نمونه (sandbox)...");
        var bytes = BuildSandboxSpreadsheet(request.TaxYear, sampleRows: 5);
        var rows = ParseWorkbookRows(bytes);

        Update(progress, MowadiyanExportStage.Importing, "ثبت خودکار فاکتورها در دیتابیس (sandbox)...");
        var imported = await UpsertRowsAsync(request, rows, ct);

        progress.TotalRows = imported;
        progress.ImportedRows = imported;
        _logger.LogInformation("Sandbox Mowadiyan export job produced {Count} sample rows.", imported);
        Update(progress, MowadiyanExportStage.Completed,
            $"تولید و ثبت {imported.ToString(CultureInfo.InvariantCulture)} فاکتور نمونه (sandbox) با موفقیت انجام شد.");
    }

    private void Update(MowadiyanPortalExportProgress progress, MowadiyanExportStage stage, string? message = null)
    {
        progress.Stage = stage;
        progress.StageText = stage.ToString();
        if (message is not null) progress.Message = message;
        progress.UpdatedAt = DateTime.Now;
    }

    private static MowadiyanPortalInitiationResult Fail(string message)
        => new() { Success = false, Message = message };
// ── Python process orchestration ───────────────────────────────────────────
    private sealed class PythonExporterResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string? ChallengeToken { get; set; }
        public string? MaskedPhone { get; set; }
        public string? Status { get; set; }
        public int Rows { get; set; }
    }

    /// <summary>
    /// Executes <c>scripts/tax_exporter.py</c> with the requested action via
    /// <see cref="System.Diagnostics.Process"/>, streaming both stdout and stderr
    /// into the logger, and parses the single JSON object the script prints to stdout.
    /// </summary>
    private async Task<PythonExporterResult> RunTaxExporterAsync(
        string action, MowadiyanSettings settings, string? otp,
        MowadiyanPortalExportRequest? request, string outDir, CancellationToken ct)
    {
        var scriptPath = ResolveScriptPath();
        var python = ResolvePythonExecutable();
        if (!System.IO.File.Exists(scriptPath))
        {
            throw new InvalidOperationException($"اسکریپت پایتون یافت نشد: {scriptPath}");
        }

        _logger.LogInformation("Starting Python tax exporter ({Action}) for username {Username}; script={Script}.",
            action, settings.PortalUsername, scriptPath);

        var psi = new ProcessStartInfo
        {
            FileName = python,
            WorkingDirectory = _env.ContentRootPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("--action");
        psi.ArgumentList.Add(action);
        psi.ArgumentList.Add("--username");
        psi.ArgumentList.Add(settings.PortalUsername ?? string.Empty);
        psi.ArgumentList.Add("--password");
        psi.ArgumentList.Add(settings.PortalPassword ?? string.Empty);
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outDir);
        if (!string.IsNullOrWhiteSpace(otp))
        {
            psi.ArgumentList.Add("--otp");
            psi.ArgumentList.Add(otp);
        }
        if (request is not null)
        {
            psi.ArgumentList.Add("--year");
            psi.ArgumentList.Add(request.TaxYear.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--period");
            psi.ArgumentList.Add(request.TaxPeriod.ToString(CultureInfo.InvariantCulture));
        }
        if (settings.IsSandbox)
        {
            psi.ArgumentList.Add("--sandbox");
        }

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
        {
            throw new InvalidOperationException("امکان اجرای فرایند پایتون وجود ندارد.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        _logger.LogInformation("Python tax exporter ({Action}) exited code {Code}.", action, process.ExitCode);
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            foreach (var line in stdout.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _logger.LogDebug("Python[{Action}] stdout: {Line}", action, line.TrimEnd());
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            foreach (var line in stderr.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _logger.LogDebug("Python[{Action}] stderr: {Line}", action, line.TrimEnd());
                }
            }
        }

        var result = ParsePythonStdout(stdout);
        if (result is null)
        {
            result = new PythonExporterResult
            {
                Success = false,
                Message = "خروجی JSON قابل قبولی از اسکریپت پایتون دریافت نشد."
            };
        }

        if (!result.Success)
        {
            var detail = !string.IsNullOrWhiteSpace(result.Message)
                ? result.Message
                : (!string.IsNullOrWhiteSpace(stderr) ? stderr : stdout);
            _logger.LogError("Python tax exporter ({Action}) failed: {Detail}", action, Truncate(detail));
        }

        // Normalize a possibly relative path returned by the script to absolute under outDir.
        if (!string.IsNullOrWhiteSpace(result.FilePath) && !Path.IsPathRooted(result.FilePath))
        {
            result.FilePath = Path.GetFullPath(Path.Combine(outDir, result.FilePath));
        }

        return result;
    }
private static PythonExporterResult? ParsePythonStdout(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout)) return null;

        var start = stdout.IndexOf('{');
        var end = stdout.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        try
        {
            using var doc = JsonDocument.Parse(stdout.Substring(start, end - start + 1));
            var root = doc.RootElement;
            return new PythonExporterResult
            {
                Success = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True,
                Message = GetPropString(root, "message") ?? GetPropString(root, "error") ?? string.Empty,
                FilePath = GetPropString(root, "filePath"),
                ChallengeToken = GetPropString(root, "challengeToken"),
                MaskedPhone = GetPropString(root, "maskedPhone"),
                Status = GetPropString(root, "status"),
                Rows = root.TryGetProperty("rows", out var r) && r.ValueKind == JsonValueKind.Number
                    ? r.GetInt32()
                    : 0
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string ResolveScriptPath()
    {
        var configured = _options.Value.PythonScriptPath;
        if (!string.IsNullOrWhiteSpace(configured) && System.IO.File.Exists(Path.GetFullPath(configured)))
        {
            return Path.GetFullPath(configured);
        }
        return Path.Combine(_env.ContentRootPath, "scripts", "tax_exporter.py");
    }

    private string ResolvePythonExecutable()
    {
        var configured = _options.Value.PythonExecutable;
        return string.IsNullOrWhiteSpace(configured) ? "python" : configured;
    }

    private string ResolveOutDir()
    {
        var configured = _options.Value.ExportOutDir;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            Directory.CreateDirectory(configured);
            return Path.GetFullPath(configured);
        }
        var dir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "mowadiyan");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Truncate(string value) => value.Length > 240 ? value[..240] : value;

    private static string? GetPropString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.GetRawText(),
                _ => null
            };
        }
        return null;
    }
// ── Workbook parsing (ClosedXML) ───────────────────────────────────────────
    /// <summary>
    /// Parses an XLSX workbook (extracting it from a ZIP archive first, when necessary)
    /// into <see cref="ParsedInvoiceRow"/> objects using a header-driven column mapping.
    /// </summary>
    private IReadOnlyList<ParsedInvoiceRow> ParseWorkbookRows(byte[] bytes)
    {
        var workbookStreams = ExtractWorkbookStreams(bytes);
        var rows = new List<ParsedInvoiceRow>();

        foreach (var stream in workbookStreams)
        {
            using (stream)
            using (var workbook = new XLWorkbook(stream))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    var range = worksheet.RangeUsed();
                    if (range is null) continue;

                    var headerRow = range.Row(1);
                    var columnMap = BuildColumnMap(headerRow);

                    var totalColumns = range.ColumnCount();
                    for (var r = 2; r <= range.RowCount(); r++)
                    {
                        var excelRow = range.Row(r);
                        var firstCell = excelRow.Cell(1).GetString().Trim();
                        if (string.IsNullOrWhiteSpace(firstCell) && IsRowEmpty(excelRow, totalColumns))
                        {
                            continue; // skip fully empty rows
                        }

                        rows.Add(ReadRow(excelRow, columnMap));
                    }
                }
            }
        }

        return rows;
    }

    /// <summary>Returns the inner .xlsx streams from either raw XLSX bytes or a ZIP archive.</summary>
    private static List<MemoryStream> ExtractWorkbookStreams(byte[] bytes)
    {
        var result = new List<MemoryStream>();
        if (bytes.Length == 0) return result;

        bool isZip = bytes.Length >= 4
                     && bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04;

        if (isZip)
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                if (!string.Equals(Path.GetExtension(entry.Name), ".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // only the inner Excel workbook(s) are relevant
                }

                var ms = new MemoryStream();
                using var source = entry.Open();
                source.CopyTo(ms);
                ms.Position = 0;
                result.Add(ms);
            }

            return result;
        }

        // Plain (non-zipped) XLSX — copy bytes into a stream.
        var direct = new MemoryStream(bytes);
        direct.Position = 0;
        result.Add(direct);
        return result;
    }
/// <summary>Builds a header-driven column-map from the first worksheet row.</summary>
    private static Dictionary<int, InvoiceField> BuildColumnMap(IXLRangeRow headerRow)
    {
        var map = new Dictionary<int, InvoiceField>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = NormalizeHeader(cell.GetString());
            if (string.IsNullOrWhiteSpace(header)) continue;

            var field = ResolveField(header);
            if (field != InvoiceField.None)
            {
                map[cell.Address.ColumnNumber] = field;
            }
        }

        return map;
    }

    /// <summary>Reads a single data row into a <see cref="ParsedInvoiceRow"/>.</summary>
    private static ParsedInvoiceRow ReadRow(IXLRangeRow excelRow, Dictionary<int, InvoiceField> columnMap)
    {
        var row = new ParsedInvoiceRow();

        // When the header is unrecognized, fall back to a positional mapping (1..N).
        var positional = columnMap.Count == 0;
        var count = 0;

        foreach (var cell in excelRow.CellsUsed())
        {
            count++;
            var field = positional
                ? (InvoiceField)count
                : (columnMap.TryGetValue(cell.Address.ColumnNumber, out var f) ? f : InvoiceField.None);

            var text = cell.GetString();
            switch (field)
            {
                case InvoiceField.InvoiceNumber: row.InvoiceNumber = NormalizeNumber(text); break;
                case InvoiceField.DateShamsi: row.DateShamsi = text.Trim(); break;
                case InvoiceField.TotalAmount: row.TotalAmount = ToDecimal(text); break;
                case InvoiceField.SubTotal: row.SubTotal = ToDecimal(text); break;
                case InvoiceField.DiscountAmount: row.DiscountAmount = ToDecimal(text); break;
                case InvoiceField.VatAmount: row.VatAmount = ToDecimal(text); break;
                case InvoiceField.DutyAmount: row.DutyAmount = ToDecimal(text); break;
                case InvoiceField.FinalAmount: row.FinalAmount = ToDecimal(text); break;
                case InvoiceField.VatRate: row.VatRate = ToDecimal(text); break;
                case InvoiceField.SellerEconomicCode: row.SellerEconomicCode = NormalizeNumber(text); break;
                case InvoiceField.BuyerEconomicCode: row.BuyerEconomicCode = NormalizeNumber(text); break;
                case InvoiceField.SellerName: row.SellerName = text.Trim(); break;
                case InvoiceField.BuyerName: row.BuyerName = text.Trim(); break;
            }
        }

        // Fallbacks for amounts that are frequently left empty.
        if (row.FinalAmount <= 0) row.FinalAmount = row.TotalAmount;
        if (row.SubTotal <= 0 && row.FinalAmount > 0) row.SubTotal = row.FinalAmount;

        return row;
    }

    private static bool IsRowEmpty(IXLRangeRow excelRow, int totalColumns)
    {
        for (var c = 1; c <= totalColumns; c++)
        {
            if (!string.IsNullOrWhiteSpace(excelRow.Cell(c).GetString()))
            {
                return false;
            }
        }
        return true;
    }
// ── Database upsert (C# orchestration) ─────────────────────────────────────
    /// <summary>Upserts parsed rows into <see cref="InvoiceRecord"/> (Purchase, IsMowadiyanSynced = true).</summary>
    private async Task<int> UpsertRowsAsync(MowadiyanPortalExportRequest request, IReadOnlyList<ParsedInvoiceRow> rows, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TaxDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var now = DateTime.Now;
        var imported = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            var invoiceNumber = row.InvoiceNumber;
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                invoiceNumber = "MOW-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
            }

            // Upsert by invoice number.
            var existing = await db.InvoiceRecords
                .FirstOrDefaultAsync(r => r.InvoiceNumber == invoiceNumber, ct);

            if (existing is null)
            {
                existing = new InvoiceRecord
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = invoiceNumber,
                    InvoiceType = "Purchase",
                    Status = "Processed",
                    IsMowadiyanSynced = true,
                    CreatedAt = now
                };
                db.InvoiceRecords.Add(existing);
            }

            ApplyParsedValues(existing, row, request);
            existing.UpdatedAt = now;
            imported++;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Mowadiyan portal import upserted {Count} invoice rows.", imported);
        return imported;
    }

    private static void ApplyParsedValues(InvoiceRecord target, ParsedInvoiceRow row, MowadiyanPortalExportRequest request)
    {
        target.InvoiceType = "Purchase";
        target.IsMowadiyanSynced = true;

        var dateShamsi = NormalizeDate(row.DateShamsi);
        if (!string.IsNullOrWhiteSpace(dateShamsi))
        {
            target.DateShamsi = dateShamsi;
            target.InvoiceDate = JalaliDateHelper.ParseShamsiToGregorian(dateShamsi);
        }

        if (row.TotalAmount > 0) target.TotalAmount = row.TotalAmount;
        if (row.SubTotal > 0) target.SubTotal = row.SubTotal;
        if (row.DiscountAmount > 0) target.DiscountAmount = row.DiscountAmount;
        if (row.VatAmount > 0) target.VatAmount = row.VatAmount;
        if (row.DutyAmount > 0) target.DutyAmount = row.DutyAmount;
        if (row.FinalAmount > 0) target.FinalAmount = row.FinalAmount;
        if (row.VatRate > 0) target.VatRate = row.VatRate;

        if (!string.IsNullOrWhiteSpace(target.Notes))
        {
            target.Notes += $" | سامانه مودیان - سال {request.TaxYear} دوره {request.TaxPeriod}";
        }
        else
        {
            target.Notes = $"وارد شده از فایل خروجی سامانه مودیان - سال {request.TaxYear} دوره {request.TaxPeriod}";
        }
    }

    /// <summary>Builds a small in-memory XLSX used by the sandbox client (offline demo).</summary>
    private static byte[] BuildSandboxSpreadsheet(int requestYear, int sampleRows)
    {
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("خرید داخلی");

        var headers = new[] { "شماره صورتحساب", "تاریخ", "مالیات", "عوارض", "مبلغ کل", "نرخ مالیات", "کد اقتصادی فروشنده" };
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        var random = new Random(requestYear);
        var baseAmt = 12_500_000m + (requestYear * 100_000);
        for (var i = 1; i <= sampleRows; i++)
        {
            var row = i + 1;
            var number = $"{requestYear}-{String.Concat(random.Next(0, 10), random.Next(0, 10), random.Next(0, 10), random.Next(0, 10))}-{i:D3}";
            var amount = baseAmt + (row * 350_000m);
            var vat = amount * 0.10m;
            var date = $"{requestYear}/{(random.Next(1, 4) + (i % 3)).ToString("00")}/{(1 + (i % 28)).ToString("00")}";

            sheet.Cell(row, 1).Value = number;
            sheet.Cell(row, 2).Value = date;
            sheet.Cell(row, 3).Value = vat;
            sheet.Cell(row, 4).Value = 0m;
            sheet.Cell(row, 5).Value = amount;
            sheet.Cell(row, 6).Value = 0.10d;
            sheet.Cell(row, 7).Value = "41118896825";
        }

        using var ms = new MemoryStream();
        book.SaveAs(ms);
        return ms.ToArray();
    }
// ── Column/header & number normalization ───────────────────────────────────
    private static string NormalizeHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return NormalizeNumber(value.Trim())
            .Replace("ي", "ی").Replace("ك", "ک")
            .ToLowerInvariant();
    }

    private static string NormalizeDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim().Replace('-', '/').Replace('.', '/');
    }

    /// <summary>Converts Persian/Arabic digits to Latin and strips thousand separators.</summary>
    private static string NormalizeNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return value
            .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
            .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7').Replace('۸', '8').Replace('۹', '9')
            .Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
            .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9')
            .Replace(",", "").Replace("٬", "");
    }

    private static decimal ToDecimal(string value)
    {
        var normalized = NormalizeNumber(value).Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return 0m;
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0m;
    }

    /// <summary>Maps a normalized header to an invoice field; returns None for unmatched headers.</summary>
    private static InvoiceField ResolveField(string header)
    {
        if (header.Contains("شماره") || header.Contains("number") || header.Contains("uid") || header.Contains("شناسه صورتحساب"))
            return InvoiceField.InvoiceNumber;
        if (header.Contains("تاریخ") || header == "date" || header.Contains("صدور"))
            return InvoiceField.DateShamsi;
        if (header.Contains("مبلغ کل") || header == "totalamount" || header == "sum" || header.Contains("مبلغ قابل پرداخت"))
            return InvoiceField.TotalAmount;
        if (header.Contains("جمع") && header.Contains("بدون") || header == "subtotal")
            return InvoiceField.SubTotal;
        if (header.Contains("تخفیف") || header == "discountamount")
            return InvoiceField.DiscountAmount;
        if ((header.Contains("مالیات") && header.Contains("عوارض")) || header == "vatamount" || (header.Contains("مالیات") && !header.Contains("نرخ")))
            return InvoiceField.VatAmount;
        if (header.Contains("عوارض") && !header.Contains("مالیات"))
            return InvoiceField.DutyAmount;
        if (header.Contains("نهایی") || header == "finalamount" || header == "total")
            return InvoiceField.FinalAmount;
        if (header.Contains("نرخ") || header == "vatrate")
            return InvoiceField.VatRate;
        if (header.Contains("کد اقتصادی") && header.Contains("فروشنده"))
            return InvoiceField.SellerEconomicCode;
        if (header.Contains("کد اقتصادی") && header.Contains("خریدار"))
            return InvoiceField.BuyerEconomicCode;
        if ((header.Contains("فروشنده") && !header.Contains("کد")) || header == "sellername")
            return InvoiceField.SellerName;
        if ((header.Contains("خریدار") && !header.Contains("کد")) || header == "buyername")
            return InvoiceField.BuyerName;
        return InvoiceField.None;
    }

    // ── Supporting types ──────────────────────────────────────────────────
    private enum InvoiceField
    {
        None = 0,
        InvoiceNumber = 1,
        DateShamsi = 2,
        TotalAmount = 3,
        SubTotal = 4,
        DiscountAmount = 5,
        VatAmount = 6,
        DutyAmount = 7,
        FinalAmount = 8,
        VatRate = 9,
        SellerEconomicCode = 10,
        BuyerEconomicCode = 11,
        SellerName = 12,
        BuyerName = 13
    }

    private sealed class ParsedInvoiceRow
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public string DateShamsi { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal DutyAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public decimal VatRate { get; set; }
        public string SellerEconomicCode { get; set; } = string.Empty;
        public string BuyerEconomicCode { get; set; } = string.Empty;
        public string SellerName { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
    }
}