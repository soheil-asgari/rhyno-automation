using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

// ============================================================================
// Request / DTO definitions for the Tax Portal output-file export.
// ============================================================================

/// <summary>
/// Parameters of an output-file export request (درخواست فایل خروجی), bound from the
/// JSON body of <c>CreatePortalExport</c> / <c>InitiatePortalLogin</c>. The fixed
/// export type is purchase invoices ("خروجی از صورتحساب‌ها") with all statuses
/// &lt;-&gt; patterns and the domestic-purchase transaction type (خرید داخلی).
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
    /// True when the portal session is already active so no SMS OTP is needed. When
    /// true, <see cref="OtpCode"/> may be empty.
    /// </summary>
    public bool AlreadyLoggedIn { get; set; }
}

// ============================================================================
// TaxExporterService — refactored, modular Tax Portal exporter.
//
// Ported from the reference Python implementation (scripts/tax_exporter.py +
// scripts/login.py) and aligned with the strict navigation & SSO flow that led to
// the existing C# implementation dropping its session (see TAX_EXPORTER_DIAGNOSIS.md).
//
// The SSO session is treated as a FIRST-CLASS resource that MUST NOT be dropped:
//   * the Chromium browser is launched with LaunchPersistentContextAsync against a
//     FIXED on-disk user-data dir (./tax_browser_profile) so cookies, LocalStorage
//     and SessionStorage survive across "initiate" -> "complete",
//   * during the authentication pipeline the SAME live context/page is reused via
//     context.Pages — no new context is created and no page is closed,
//   * the post-OTP navigation to /invoice/outputFiles happens ONLY AFTER the
//     browser has naturally redirected to tp.tax.gov.ir (a 30s explicit wait),
//   * every Persian selector is glyph-normalized via PersianTextNormalizer.NormalizeText
//     so Arabic yeh/kaf variants ("ي" vs "ی", "ك" vs "ک") and ZWNJs never break matching,
//   * CAPTCHA solving is routed through an asynchronous HttpClient OCR wrapper
//     (ExternalCaptchaOcrClient) with a retry loop (up to 3 attempts) and a captcha
//     refresh click-handler when recognition / verification fails.
// ============================================================================

/// <summary>Parameters of a single output-file export run (درخواست فایل خروجی).</summary>
public sealed class TaxExportParameters
{
    /// <summary>Portal username (کد ملی / username).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Portal password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Six-digit SMS OTP code supplied by the operator.</summary>
    public string OtpCode { get; set; } = string.Empty;

    /// <summary>Fiscal year, e.g. 1405.</summary>
    public int TaxYear { get; set; }

    /// <summary>Fiscal period / season 1..4.</summary>
    public int TaxPeriod { get; set; }

    /// <summary>Directory where the downloaded .xlsx/.csv is written.</summary>
    public string OutputDirectory { get; set; } = string.Empty;
}

/// <summary>Status emitted by <see cref="ITaxExporterService.InitiateAsync"/>.</summary>
public enum TaxExportInitiationStatus
{
    /// <summary>Session was already active on tp.tax.gov.ir — no SMS needed.</summary>
    AlreadyLoggedIn,

    /// <summary>SMS OTP was sent; the live session is parked on the OTP screen awaiting the code.</summary>
    AwaitingOtp,

    /// <summary>Captcha rejected / login failed; the OTP was never sent.</summary>
    OtpNotSent
}

/// <summary>Result of the initiate (login + OTP-send) step.</summary>
public sealed class TaxExportInitiationResult
{
    public bool Success { get; set; }
    public TaxExportInitiationStatus Status { get; set; }
    public string? Message { get; set; }

    /// <summary>Correlation token keying the long-lived persistent browser session.</summary>
    public string? SessionToken { get; set; }
    public string? MaskedPhone { get; set; }

    /// <summary>
    /// True when the operator must still enter an SMS code to finish authentication; false
    /// when the session is already active so the OTP step can be skipped entirely (e.g. the
    /// portal logged the user straight into the dashboard without requesting a code).
    /// </summary>
    public bool RequiresOtp { get; set; }
}

/// <summary>Final result of the complete (OTP + export + download) step.</summary>
public sealed class TaxExportDownloadResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? OutputPath { get; set; }
}
/// <summary>OCR caption solver contract. Sends CAPTCHA PNG bytes to an OCR backend and
/// returns the recognised text (or empty when unrecognized).</summary>
public interface ICaptchaOcrClient
{
    Task<string> SolveAsync(byte[] imageBytes, CancellationToken cancellationToken);
}

/// <summary>
/// Service contract for the Tax Portal export pipeline, mirroring the reference
/// Python exporter's two-step (initiate / complete) model without ever dropping
/// the SSO session between the two calls.
/// </summary>
public interface ITaxExporterService
{
    /// <summary>Step 1 — navigate to my.tax.gov.ir/Login, reuse the persistent browser
    /// context, fill credentials + CAPTCHA, submit, then wait explicitly for the OTP
    /// input phase. Returns AlreadyLoggedIn when the cached profile is authenticated.</summary>
    Task<TaxExportInitiationResult> InitiateAsync(TaxExportParameters parameters, CancellationToken cancellationToken);

    /// <summary>Step 2 — enter the OTP, wait up to 30s for the natural SSO redirect to
    /// tp.tax.gov.ir, verify the URL, navigate strictly to /invoice/outputFiles, and only
    /// then run the درخواست فایل خروجی modal automation + download.</summary>
    Task<TaxExportDownloadResult> CompleteAsync(
        string sessionToken, string otpCode, int taxYear, int taxPeriod, CancellationToken cancellationToken);
}

/// <summary>CAPTCHA OCR backed by a configurable external HTTP endpoint. Sends a
/// multipart/form-data image to <c>Captcha:OcrBaseUrl</c>. Tolerates config that is
/// unset/missing (returns empty so the caller can retry/refresh) and never throws on
/// non-2xx — it just returns empty.</summary>
public sealed class ExternalCaptchaOcrClient : ICaptchaOcrClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<CaptchaOcrOptions> _options;
    private readonly ILogger<ExternalCaptchaOcrClient> _logger;

    public ExternalCaptchaOcrClient(
        HttpClient httpClient,
        IOptions<CaptchaOcrOptions> options,
        ILogger<ExternalCaptchaOcrClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<string> SolveAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.OcrBaseUrl) || imageBytes is null || imageBytes.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(options.ContentType ?? "image/png");
            fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = options.FieldName ?? "image",
                FileName = $"captcha_{Guid.NewGuid():N}.png"
            };
            content.Add(fileContent);

            using var request = new HttpRequestMessage(HttpMethod.Post, options.OcrBaseUrl)
            {
                Content = content
            };
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Api-Key", options.ApiKey);
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CAPTCHA OCR endpoint returned {Status}.", response.StatusCode);
                return string.Empty;
            }

            return await ExtractTextAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CAPTCHA OCR request failed: {Message}", ex.Message);
            return string.Empty;
        }
    }

    private static async Task<string> ExtractTextAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var code = FindCode(doc.RootElement);
            return (code ?? string.Empty).Trim().Trim('"').Trim();
        }
        catch (JsonException)
        {
            var trimmed = json.Trim().Trim('"', '\'', ' ');
            return IsLikelyCode(trimmed) ? trimmed : string.Empty;
        }
    }

    private static string? FindCode(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.String)
        {
            return IsLikelyCode(element.GetString()) ? element.GetString() : null;
        }

        if (element.ValueKind is JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var name = NormalizeKey(prop.Name);
                if (name is "code" or "text" or "value" or "captcha" or "result" or "data")
                {
                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        var nested = FindCode(prop.Value);
                        if (!string.IsNullOrWhiteSpace(nested)) return nested;
                    }
                    else if (prop.Value.ValueKind is JsonValueKind.String)
                    {
                        var s = prop.Value.GetString();
                        if (IsLikelyCode(s)) return s;
                    }
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    var nested = FindCode(prop.Value);
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
            }
        }
        else if (element.ValueKind is JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindCode(item);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }

        return null;
    }

    private static string NormalizeKey(string name)
        => name.ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty);

    /// <summary>CAPTCHA codes are typically 4–6 alphanumeric characters.</summary>
    private static bool IsLikelyCode(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Length is >= 4 and <= 10;
}

/// <summary>Configuration for the external CAPTCHA OCR endpoint.</summary>
public sealed class CaptchaOcrOptions
{
    public const string SectionName = "Captcha";

    /// <summary>Absolute URL of the external OCR API (multipart image upload). Empty disables it.</summary>
    public string? OcrBaseUrl { get; set; }

    /// <summary>Optional API key sent as the X-Api-Key header.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Multipart field name carrying the image bytes (default "image").</summary>
    public string? FieldName { get; set; }

    /// <summary>Content-Type of the uploaded image (default "image/png").</summary>
    public string? ContentType { get; set; }
}

/// <summary>
/// Implements the reference Python exporter flow (scripts/tax_exporter.py +
/// scripts/login.py) as a clean, async, modular C# service. Owns the strict
/// navigation/SSO ordering, the persistent Chromium profile, the glyph-normalized
/// Persian selectors, the HttpClient CAPTCHA OCR retry loop and the modal + polling
/// + ExpectDownloadAsync export path.
/// </summary>
public sealed class TaxExporterService : ITaxExporterService
{
    private const string PortalBase = "https://tp.tax.gov.ir";
    private const string LoginBase = "https://my.tax.gov.ir";
    private const string OutputFilesUrl = "https://tp.tax.gov.ir/invoice/outputFiles";
    private const string SuccessStatus = "موفق";
    private const string RequestOutputButton = "درخواست فایل خروجی";
    private const string DomesticPurchase = "خرید داخلی";

    private const int MaxCaptchaAttempts = 3;
    private const int OtpRedirectTimeoutSeconds = 30;
    private const int PollIntervalSeconds = 10;
    private const int PollDeadlineSeconds = 300;

    private static readonly IReadOnlyDictionary<int, string> PeriodLabels
        = new Dictionary<int, string>
        {
            [1] = "بهار",
            [2] = "تابستان",
            [3] = "پاییز",
            [4] = "زمستان"
        };

    private static readonly string[] OtpFieldSelectors =
    {
        "input[placeholder*='پیامک']",
        "input[autocomplete='one-time-code']",
        "#OtpCode",
        "#otp",
        "#otpCode",
        "input[name*='otp' i]",
        "input[name*='otp']",
        "input[name*='code' i]",
        "input[name*='code']",
        "input#otp",
        "input.CodeConfirm",
        "input[name*='verify']",
        "input[inputmode='numeric']"
    };

    private static readonly string[] OtpSubmitSelectors =
    {
        // The real «تایید» (blue) button is a <a id="OtpButton" class="btn-input doubleresist">,
        // NOT a <button>. Match it exactly first so we never click the orange «انصراف» link by accident.
        "#OtpButton",
        "a#OtpButton",
        "a#OtpButton.btn-input",
        "a.btn-input#OtpButton",
        "a.btn-input.doubleresist",
        "a.btn-input",
        "button#OtpButton",
        "button:has-text('تایید')",
        "a:has-text('تایید')",
        "input[type='submit']"
    };

    private static readonly string[] CaptchaRefreshSelectors =
    {
        "a[onclick*='refresh' i]",
        "button[onclick*='refresh' i]",
        "#captchaRefresh",
        "#refreshCaptcha",
        "img[src*='reset']",
        "img[srcset*='reset']",
        "img[onclick*='captchaReset' i]",
        "img[class*='reset']",
        "img[alt*='جدید']",
        "img[alt*='جديد']"
    };

    private readonly ConcurrentDictionary<string, (IBrowserContext Context, IPlaywright Playwright)> _sessions = new();
    private readonly ICaptchaOcrClient _captchaOcr;
    private readonly IOptions<MowadiyanOptions> _options;
    private readonly ILogger<TaxExporterService> _logger;
    private string _profileDir = string.Empty;
    private string _lastOutDir = string.Empty;

    public TaxExporterService(
        ICaptchaOcrClient captchaOcr,
        IOptions<MowadiyanOptions> options,
        ILogger<TaxExporterService> logger)
    {
        _captchaOcr = captchaOcr;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TaxExportInitiationResult> InitiateAsync(
        TaxExportParameters parameters, CancellationToken cancellationToken)
    {
        _profileDir = ResolveProfileDir();
        _lastOutDir = string.IsNullOrWhiteSpace(parameters.OutputDirectory)
            ? ResolveDefaultOutputDir()
            : parameters.OutputDirectory;

        // GUARD (resource-leak fix): this later relaunches a persistent context on the SAME
        // profile dir. If any previous browser from an earlier request is still open, launching
        // a second instance against the same on-disk profile either throws (browser_data LOCK)
        // or silently orphans the old Chromium process in RAM. Close all parked sessions first.
        if (_sessions.Count > 0)
        {
            _logger.LogInformation(
                "Closing {Count} previously parked Tax browser session(s) before a fresh login initiate.",
                _sessions.Count);
            foreach (var kv in _sessions.ToArray())
            {
                await DisposeSessionContextAsync(kv.Value.Context, kv.Value.Playwright);
                _sessions.TryRemove(kv.Key, out _);
            }
        }

        var playwright = await PreparePlaywrightAsync(cancellationToken);
        var context = await playwright.Chromium.LaunchPersistentContextAsync(_profileDir, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = _options?.Value?.Headless ?? false,
            Locale = "fa-IR",
            Args = new[] { "--disable-blink-features=AutomationControlled" },
            IgnoreDefaultArgs = new[] { "--enable-automation" }
        });

        try
        {
            // Reuse the existing active page if one exists — never create a second context.
            // Prefer a Tax-domain tab (portal / login) over stray about:blank tabs restored
            // by the persistent profile.
            var page = await SelectSessionPageAsync(context, cancellationToken) ?? await context.NewPageAsync();

            // Step 1 — navigate to the SSO login page.
            await page.GotoAsync($"{LoginBase}/Login",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded,
                new PageWaitForLoadStateOptions { Timeout = 30000 });

            // If the cached profile is already authenticated, SSO redirects to the portal.
            // Use the broad IsLoggedInOrOnPortalAsync so a direct login onto the authenticated
            // my.tax.gov.ir dashboard (/Page/...) is recognized, not just tp.tax.gov.ir.
            if (await IsLoggedInOrOnPortalAsync(page))
            {
                _sessions.TryAdd(Guid.NewGuid().ToString("N"), (context, playwright));
                return new TaxExportInitiationResult
                {
                    Success = true,
                    Status = TaxExportInitiationStatus.AlreadyLoggedIn,
                    Message = "نشست از قبل فعال است، نیازی به دریافت کد SMS نیست.",
                    RequiresOtp = false
                };
            }

            // Step 2 — fill credentials + CAPTCHA and submit to trigger the SMS OTP.
            var otpTriggered = await FillLoginAndTriggerOtpAsync(page, parameters, cancellationToken);
            if (!otpTriggered)
            {
                // The tax portal sometimes skips the OTP step entirely and logs the user
                // straight into the dashboard (my.tax.gov.ir/Page/Dashboard) using a still-valid
                // session / cookies. In that case we must NOT report a failure and must NOT wait
                // for an OTP box that will never appear — treat it as an already-active session.
                _logger.LogInformation(
                    "No OTP screen appeared after login; re-checking whether the user was logged in directly (URL={Url}).",
                    GetPageUrl(page));
                if (await IsLoggedInOrOnPortalAsync(page))
                {
                    _sessions.TryAdd(Guid.NewGuid().ToString("N"), (context, playwright));
                    return new TaxExportInitiationResult
                    {
                        Success = true,
                        Status = TaxExportInitiationStatus.AlreadyLoggedIn,
                        Message = "ورود بدون نیاز به کد پیامک انجام شد؛ نشست فعال است.",
                        RequiresOtp = false
                    };
                }

                return new TaxExportInitiationResult
                {
                    Success = false,
                    Status = TaxExportInitiationStatus.OtpNotSent,
                    Message = "کد کپچا وارد نشد یا توسط سرور رد شد و پیامک OTP صادر نشد."
                };
            }

            // Step 3 — park the LIVE session on the OTP screen; do NOT close the page or
            // context (closing drops the SSO connection and invalidates the pending OTP).
            var sessionToken = "persistent-challenge-" + Guid.NewGuid().ToString("N");
            _sessions.TryAdd(sessionToken, (context, playwright));

            // Debug aid: capture the OTP/verification screen right after the SMS is sent so
            // the operator can confirm which portal screen/status is rendered (and inspect the
            // page text in logs even if the image is unreadable).
            await CaptureOtpDebugSnapshotAsync(page, _lastOutDir, cancellationToken);

            return new TaxExportInitiationResult
            {
                Success = true,
                Status = TaxExportInitiationStatus.AwaitingOtp,
                SessionToken = sessionToken,
                Message = "کد پیامک با موفقیت صادر شد. لطفاً کد شش‌رقمی را وارد نمایید.",
                MaskedPhone = await TryExtractMaskedPhoneAsync(page, cancellationToken),
                RequiresOtp = true
            };
        }
        catch (OperationCanceledException)
        {
            await DisposeSessionContextAsync(context, playwright);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tax Portal login initiate failed: {Message}", ex.Message);
            await DisposeSessionContextAsync(context, playwright);
            return new TaxExportInitiationResult
            {
                Success = false,
                Status = TaxExportInitiationStatus.OtpNotSent,
                Message = $"ورود به سامانه مودیان ناموفق بود: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<TaxExportDownloadResult> CompleteAsync(
        string sessionToken, string otpCode, int taxYear, int taxPeriod, CancellationToken cancellationToken)
    {
        // Resolve the live persistent session parked in InitiateAsync. IMPORTANT: we MUST
        // continue on that SAME live browser/page instance — never close parked sessions
        // and never re-launch the profile here (re-launching drops cookies/LocalStorage,
        // the SSO connection and the open dashboard, which is exactly the failure mode we
        // are fixing). If no live session exists, fail gracefully instead of relaunching.
        var context = ResolveContext(sessionToken, out var ownsContext);
        var completedSuccessfully = false;
        if (context is null)
        {
            _logger.LogWarning(
                "No live Tax browser session found for token '{Token}'. " +
                "Session must be initiated (InitiateAsync) before it can be completed.",
                sessionToken);
            return new TaxExportDownloadResult
            {
                Success = false,
                Message = "نشست مرورگر فعالی برای این عملیات یافت نشد. ابتدا عملیات ورود را مجدداً اجرا کنید."
            };
        }

        try
        {
            // Reuse the existing active page — never create a new one during auth. The
            // persistent profile can restore several tabs, so a naive FirstOrDefault() may
            // return an unrelated leftover about:blank tab while the real OTP/portal page
            // sits in another tab. Prefer whichever tab actually hosts a visible OTP box;
            // fall back to URL-based selection for the authenticated portal.
            var page = await FindPageWithOtpFieldAsync(context, cancellationToken);
            if (page is null)
            {
                page = await SelectSessionPageAsync(context, cancellationToken) ?? await context.NewPageAsync();
            }

            // Smart pre-check: the operator may have already typed the OTP into the live
            // browser window and clicked «تایید» themselves. In that case the page is no
            // longer on the OTP screen — we must NOT try to refill the OTP field and we
            // must NOT raise the dreaded «کادر ورود کد تأیید (OTP) در صفحه پیدا نشد» error.
            // CRITICAL FIX: a blank URL (e.g. about:blank) is NEVER treated as authenticated.
            // The old heuristic ("not on portal + no visible OTP field => assume already
            // authenticated") wrongly promoted a blank/stray tab, silently skipped the OTP,
            // and then drove an unauthenticated blank browser straight to outputFiles.
            // //////////////////////////////////////////////////////////////////
            // ۱. First determine whether the user is already authenticated (or an
            // active persistent session exists) BEFORE touching the OTP screen. If so,
            // the OTP step is skipped entirely — we must never look for an OTP box the
            // page has already passed (it sits on the dashboard/portal).
            var isAlreadyLoggedIn = await IsLoggedInOrOnPortalAsync(page);

            if (isAlreadyLoggedIn)
            {
                // نشست فعال است — کاربر از قبل وارد شده و عملیات با کد OTP لازم نیست.
                _logger.LogInformation(
                    "نشست فعال است و کاربر در داشبورد/پرتال حضور دارد. پردازش OTP اسکیپ شد.");
            }
            else
            {
                // ۲. Only when the user is NOT authenticated do we look for the OTP
                // field on the SAME live page.
                var otpField = await FindOtpFieldAsync(page);
                if (otpField is null)
                {
                    var pageError = await TryExtractPageErrorAsync(page, cancellationToken);
                    return new TaxExportDownloadResult
                    {
                        Success = false,
                        Message = "کادر ورود کد تأیید (OTP) در صفحه پیدا نشد و نشست فعالی نیز یافت نشد."
                                  + (string.IsNullOrWhiteSpace(pageError) ? string.Empty : $" پیام سامانه: {pageError}")
                    };
                }

                await otpField.DisposeAsync();
                await FillOtpAsync(page, otpCode, cancellationToken);
                await SubmitOtpAsync(page, cancellationToken);

                // Wait naturally (30s) for the SSO redirect — either straight to
                // tp.tax.gov.ir or back to the authenticated my.tax.gov.ir dashboard.
                var loggedIn = await WaitForPortalRedirectAsync(page, cancellationToken);
                if (!loggedIn)
                {
                    var pageError = await TryExtractPageErrorAsync(page, cancellationToken);
                    _logger.LogWarning(
                        "OTP rejected or redirect timed out. Page error: {Error}",
                        string.IsNullOrWhiteSpace(pageError) ? "(none)" : pageError);
                    await CaptureOtpDebugSnapshotAsync(page, _lastOutDir, cancellationToken);
                    return new TaxExportDownloadResult
                    {
                        Success = false,
                        Message = "احراز هویت با کد پیامک ناموفق بود و انتقال به پرتال انجام نشد."
                                  + (string.IsNullOrWhiteSpace(pageError) ? string.Empty : $" پیام سامانه: {pageError}")
                    };
                }
            }

            // ۳. Now (in both paths — with or without OTP) the user is authenticated.
            // The tax portal frequently keeps the user on the my.tax.gov.ir dashboard;
            // if we are not on tp.tax.gov.ir yet, click «ورود به کارپوشه تجاری» so the
            // session truly reaches the business dashboard before we drive the explicit
            // navigation to the output-files page below.
            if (!IsUrlOnPortal(GetPageUrl(page)))
            {
                _logger.LogInformation("در حال کلیک روی کارپوشه تجاری یا انتقال به tp.tax.gov.ir...");
                await TryOpenBusinessDashboardAsync(page, cancellationToken);
            }

            _logger.LogInformation("در حال هدایت به صفحه خروجی صورتحساب‌ها ({Url})...", OutputFilesUrl);

            // Step 6 — ONLY after authentication is confirmed, navigate strictly to outputFiles.
            // We navigate in the SAME tab the operator is watching, never in a new tab — they
            // explicitly asked to keep the visible browser on the right page.
            await page.GotoAsync(OutputFilesUrl,
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded,
                new PageWaitForLoadStateOptions { Timeout = 30000 });

            // Step 7 — modal automation: open, select خرید داخلی, select-all checkboxes, fiscal period.
            await EnsureExportModalAsync(page, cancellationToken);
            await SelectDomesticPurchaseRadioAsync(page, cancellationToken);
            await SelectAllCheckboxesAsync(page, cancellationToken);
            await SetTaxYearAndPeriodAsync(page, taxYear, taxPeriod, cancellationToken);
            await ClickSubmitExportRequestAsync(page, cancellationToken);

            // Step 8 — poll the 1st table row every 10s (up to 5 min) until «موفق».
            await PollUntilSuccessAsync(page, cancellationToken);

            // Step 9 — trigger and capture the download via ExpectDownloadAsync.
            var outputPath = await DownloadExportFileAsync(page, cancellationToken);
            completedSuccessfully = true;
            return new TaxExportDownloadResult
            {
                Success = true,
                Message = "دانلود فایل خروجی با موفقیت انجام شد.",
                OutputPath = outputPath
            };
        }
        catch (OperationCanceledException)
        {
            // The request was aborted (operator closed the browser tab / app). Cleanup in the
            // finally block disposes this context so no Chromium process or profile lock leaks.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tax Portal export completion failed: {Message}", ex.Message);
            return new TaxExportDownloadResult
            {
                Success = false,
                Message = $"تکمیل عملیات خروجی سامانه مودیان ناموفق بود: {ex.Message}"
            };
        }
        finally
        {
            // IMPORTANT: the operator explicitly asked to keep the visible browser open after
            // a SUCCESSFUL operation (so they can verify the download). To avoid leaking the
            // Chromium process and leaving the profile dir locked (issue #3), we DO dispose the
            // browser when the operation FAILED. On success we leave the live parked session
            // (and its playwright) intact so the SAME browser stays usable.
            if (!completedSuccessfully)
            {
                if (context is not null)
                {
                    // Resolve the owning playwright parked in the registry and dispose it fully.
                    var owning = _sessions.TryGetValue(sessionToken, out var parked)
                        ? parked.Playwright
                        : null;
                    await DisposeSessionContextAsync(context, owning);
                }
                _sessions.TryRemove(sessionToken, out _);
            }
            else if (ownsContext)
            {
                // Success + we relaunched it ourselves: nothing parked from InitiateAsync, so
                // drop the registry entry (fresh token next time) but keep the browser visible.
                _sessions.TryRemove(sessionToken, out _);
            }
        }
    }

    /// <summary>Returns true if a visible OTP entry input is on the current page.</summary>
    private static async Task<bool> IsOtpInputVisibleAsync(IPage page)
    {
        var field = await FindOtpFieldAsync(page);
        if (field is null)
        {
            return false;
        }
        await field.DisposeAsync();
        return true;
    }

    /// <summary>Fill username/password + CAPTCHA and submit, with a captcha-aware retry loop
    /// (up to <see cref="MaxCaptchaAttempts"/>). Returns true once the SMS OTP screen appears.</summary>
    private async Task<bool> FillLoginAndTriggerOtpAsync(
        IPage page, TaxExportParameters parameters, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxCaptchaAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            await FillBySelectorsAsync(page,
                new[] { "input#nationalcode", "input[name='nationalcode']", "input[name='NationalCode']",
                        "input[name='username']", "input#username", "input[type='text']",
                        "input[placeholder*='کد ملی']", "input[placeholder*='شماره ملی']", "input[placeholder*='شناسه ملی']" },
                parameters.Username);
            await FillBySelectorsAsync(page,
                new[] { "input[type='password']", "input[name='password']" }, parameters.Password);

            // Solve the CAPTCHA via the external OCR client, retrying up to 3 times with a
            // refresh click between attempts.
            var captchaCode = await SolveCaptchaWithRetryAsync(page, ct);
            if (string.IsNullOrWhiteSpace(captchaCode))
            {
                return false;
            }

            await FillBySelectorsAsync(page,
                new[] { "input#CaptchaCode", "input[name='CaptchaCode']", "input[placeholder*='تصویر امنیتی']", "input[placeholder*='تصویر']", "input[name='captcha']", "input[name='captcha_value']" },
                captchaCode);

            await SubmitLoginAsync(page);
            var reachedOtp = await WaitForOtpScreenAsync(page, TimeSpan.FromSeconds(15), ct);
            if (reachedOtp)
            {
                return true;
            }

            await RefreshCaptchaIfRejectedAsync(page);
            await Task.Delay(800, ct);
        }

        return false;
    }

    /// <summary>CAPTCHA OCR with an internal retry loop (up to <see cref="MaxCaptchaAttempts"/>),
    /// refreshing the image between attempts when OCR returns empty.</summary>
    private async Task<string> SolveCaptchaWithRetryAsync(IPage page, CancellationToken ct)
    {
        for (var ocrAttempt = 1; ocrAttempt <= MaxCaptchaAttempts; ocrAttempt++)
        {
            ct.ThrowIfCancellationRequested();

            if (ocrAttempt > 1)
            {
                await RefreshCaptchaIfRejectedAsync(page);
            }

            var imageBytes = await TryReadCaptchaImageAsync(page);
            if (imageBytes is null || imageBytes.Length == 0)
            {
                break;
            }

            var code = await _captchaOcr.SolveAsync(imageBytes, ct);
            if (!string.IsNullOrWhiteSpace(code))
            {
                return code;
            }

            _logger.LogWarning("CAPTCHA OCR attempt {Attempt} returned empty; refreshing image.", ocrAttempt);
            await Task.Delay(600, ct);
        }

        return string.Empty;
    }

    /// <summary>Fill the first matching visible element by typing (dispatches key events).
    /// Uses the locator-based (non-obsolete) <c>PressSequentiallyAsync</c> API.</summary>
    private static async Task FillBySelectorsAsync(IPage page, IReadOnlyList<string> selectors, string value)
    {
        foreach (var selector in selectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                if (await locator.CountAsync() == 0 || !await locator.IsVisibleAsync())
                {
                    continue;
                }

                await locator.FillAsync(string.Empty);
                await locator.PressSequentiallyAsync(
                    value ?? string.Empty,
                    new LocatorPressSequentiallyOptions { Delay = 30 });
                return;
            }
            catch { /* try the next selector */ }
        }
    }

    /// <summary>Best-effort screenshot of the visible CAPTCHA image -> PNG bytes.</summary>
    private static async Task<byte[]?> TryReadCaptchaImageAsync(IPage page)
    {
        const string selectors =
            "#img-captcha, img[src*='captcha'], img[src*='Captcha'], img[src*='getCaptcha'], img[src*='get-captcha']," +
            " img[srcset*='captcha'], img[srcset*='Captcha'], img[srcset*='get-captcha']," +
            " #simple-captcha img, #captchaImage, .captcha-img, .captcha-group img, .captcha img";
        try
        {
            var img = await page.QuerySelectorAsync(selectors)
                ?? await page.QuerySelectorAsync(".captcha-group img, .captcha img, [class*='captcha'] img, [class*='image'] img");
            if (img is null)
            {
                return null;
            }

            // The portal's captcha <img> uses srcset="/get-captcha-image/" and sits inside a
            // .captcha-group/.captcha wrapper (no plain `src`). Screenshot whatever element we
            // found so the OCR gets real pixels rather than an empty byte array.
            var bytes = await img.ScreenshotAsync();
            await img.DisposeAsync();
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Click a CAPTCHA refresh control (if any) so a new image is rendered.</summary>
    private static async Task RefreshCaptchaIfRejectedAsync(IPage page)
    {
        foreach (var selector in CaptchaRefreshSelectors)
        {
            try
            {
                var el = await page.QuerySelectorAsync(selector);
                if (el is not null)
                {
                    await el.ClickAsync(new ElementHandleClickOptions { Timeout = 3000 });
                    await el.DisposeAsync();
                    break;
                }
            }
            catch { /* try next refresh selector */ }
        }
    }

    /// <summary>Submit the active login form via clicking the real submit control.
    ///
    /// The national gateway (my.tax.gov.ir) renders its «ورود به درگاه ملی» button NOT as a
    /// &lt;button&gt; but as a &lt;label for="submit" class="button doubleresist"&gt; whose text comes
    /// solely from a CSS ::before rule (no text node in the DOM), pointing at a hidden
    /// &lt;input id="submit" type="submit"&gt;. The portal also prevents the default Enter-key
    /// behaviour on the national-ID / password fields (it only moves focus between tabbable
    /// elements), so pressing Enter never actually submits the form.
    ///
    /// Because of all this, neither `button:has-text('ورود')` nor `a:has-text(...)` nor a bare
    /// Enter press ever triggers submission. We therefore (in priority order):
    ///   1. click the visible &lt;label for="submit" class="button"&gt; (which activates the input it
    ///      forwards to, submitting the form — the exact action a real user performs), then
    ///   2. click the hidden `#submit` / `input[type='submit']` directly as a fallback, then
    ///   3. press Enter on the CAPTCHA field (harmless extra attempt).
    /// The label click is emitted regardless of the ::before text (which ITextSnapshot cannot see),
    /// so glyph normalization of the Persian text is not relied on here.</summary>
    private static async Task SubmitLoginAsync(IPage page)
    {
        if (await ClickLoginButtonAsync(page))
        {
            return;
        }

        var field = await page.QuerySelectorAsync("input#CaptchaCode, input[type='password']");
        if (field is not null)
        {
            try { await field.PressAsync("Enter"); }
            finally { await field.DisposeAsync(); }
        }
    }

    /// <summary>Try to click the real login submit control (label-for-input or the hidden submit).</summary>
    private static async Task<bool> ClickLoginButtonAsync(IPage page)
    {
        var labelSubmit = await FirstVisibleAsync(page, new[]
        {
            "label[for='submit']",
            "label.button[for='submit']",
            "label[for='submit'].button",
            "label[for='submit'].doubleresist",
        });
        if (labelSubmit is not null)
        {
            try
            {
                await labelSubmit.ClickAsync(new ElementHandleClickOptions { Timeout = 6000 });
                return true;
            }
            catch { }
            finally { await labelSubmit.DisposeAsync(); }
        }

        var rawSubmit = await FirstVisibleAsync(page, new[]
        {
            "input#submit[type='submit']",
            "input[type='submit']",
            "input#submit",
            "#submit",
        });
        if (rawSubmit is not null)
        {
            try
            {
                // The input is hidden; force-activate it so the form is submitted (same as the
                // label forwarding a click to it / a real user pressing it).
                await rawSubmit.EvaluateAsync("(el) => { try { el.click(); } catch { el.dispatchEvent(new MouseEvent('click', { bubbles: true })); } }");
                return true;
            }
            catch { }
            finally { await rawSubmit.DisposeAsync(); }
        }

        return false;
    }

    /// <summary>Poll the page until a strong OTP input is present (mirrors login.py
    /// _wait_for_otp_field). Aborts early when a visible login-error message appears.</summary>
    private static async Task<bool> WaitForOtpScreenAsync(IPage page, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var strong = await FindOtpFieldAsync(page);
            if (strong is not null)
            {
                await strong.DisposeAsync();
                return true;
            }

            var body = await SafePageTextAsync(page);
            if (Body.HasOtpKeywords(body))
            {
                var numeric = await FindOtpFieldAsync(page);
                if (numeric is not null)
                {
                    await numeric.DisposeAsync();
                    return true;
                }
            }

            if (Body.HasErrorMarkers(body))
            {
                return false;
            }

            await Task.Delay(500, ct);
        }

        return false;
    }

    /// <summary>Fill the OTP into the live page using the reference field selectors.</summary>
    private static async Task FillOtpAsync(IPage page, string otp, CancellationToken ct)
    {
        var field = await FindOtpFieldAsync(page);
        if (field is null)
        {
            throw new InvalidOperationException("کادر ورود کد تأیید (OTP) در صفحه پیدا نشد. لطفاً مطمئن شوید کد پیامک درخواست شده و مرورگر روی صفحهٔ ورود کد تأیید قرار دارد.");
        }

        // Normalize the code to ASCII digits. The operator may have typed Persian /
        // Arabic-Indic digits (۰-۹ / ٠-٩) which the portal's /Auth/Confirm endpoint
        // rejects. PersianTextNormalizer folds those glyphs to ASCII 0-9; we then keep
        // only ASCII digits so any stray whitespace/separators never corrupt the code.
        var normalized = PersianTextNormalizer.NormalizeText(otp ?? string.Empty);
        normalized = new string(normalized.Where(char.IsAsciiDigit).ToArray());

        try
        {
            // The OTP input is an element handle (found via FindOtpFieldAsync) and element
            // handles expose no non-obsolete PressSequentiallyAsync in Microsoft.Playwright
            // 1.62 — so clear + focus it and insert the digits through the page keyboard
            // (fires an input event, matching what TypeAsync used to do) with no obsolete API.
            await field.FillAsync(string.Empty);
            await field.FocusAsync();
            await page.Keyboard.InsertTextAsync(normalized);
        }
        finally
        {
            await field.DisposeAsync();
        }
    }

    /// <summary>Submit the OTP on the SSO page. Mirrors login.py verify_otp, but invokes the
    /// portal's own <c>OTPConfirm()</c> function directly as the primary trigger.</summary>
    private static async Task SubmitOtpAsync(IPage page, CancellationToken ct)
    {
        // The portal's «تایید» control is an <a id="OtpButton" class="btn-input doubleresist"
        // onclick="OTPConfirm()">. The portal also binds a jQuery mousedown handler on
        // .doubleresist that calls preventDefault() + stopImmediatePropagation(), which can
        // swallow Playwright's synthetic mouse click and leave the OTP unsubmitted. Invoking
        // the portal's own OTPConfirm() directly is therefore the most reliable trigger — it
        // is exactly what the inline onclick does, without any mouse-event interference.
        var invoked = false;
        try
        {
            await page.EvaluateAsync("OTPConfirm()");
            invoked = true;
        }
        catch
        {
            invoked = false;
        }

        if (!invoked)
        {
            // Fallback: click the submit control, then press Enter inside the OTP input
            // (the page binds a document-level keydown handler that calls OTPConfirm()).
            var submit = await FirstVisibleAsync(page, OtpSubmitSelectors);
            if (submit is not null)
            {
                try { await submit.ClickAsync(new ElementHandleClickOptions { Timeout = 6000 }); }
                finally { await submit.DisposeAsync(); }
            }
            else
            {
                var field = await FirstVisibleAsync(page, new[] { "input[name*='otp']", "input.CodeConfirm", "input#otp" });
                if (field is not null)
                {
                    await field.PressAsync("Enter");
                    await field.DisposeAsync();
                }
            }
        }

        // Give the portal's /Auth/Confirm AJAX a moment to complete. Its success handler
        // navigates to /Page/Dashboard and then on to tp.tax.gov.ir; the redirect wait in
        // the caller handles the actual navigation.
        await Task.Delay(1500, ct);
    }

    /// <summary>
    /// True when the authenticated state is confirmed on the CURRENT page: either we are
    /// directly on the business portal (tp.tax.gov.ir), or we are on the SSO dashboard
    /// (my.tax.gov.ir, incl. /Page/ routes) and the page clearly shows the post-login
    /// dashboard (کارپوشه / خروج / «ورود به کارپوشه تجاری»). Before deciding, the current
    /// URL is inspected and — when the DOM is still loading — we wait a short moment so a
    /// just-navigated page (e.g. an OTP redirect that is mid-render) isn't misjudged as
    /// logged-out. A blank or unrelated URL is never treated as authenticated.
    /// </summary>
    private static async Task<bool> IsLoggedInOrOnPortalAsync(IPage page)
    {
        const StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;

        // Inspect the current URL first.
        var url = GetPageUrl(page);

        // If the DOM is still loading (page navigation in-flight), give it a brief moment
        // so the redirected screen (portal / logged-in dashboard) has time to render before
        // we decide. Best-effort: never throws.
        await SafeWaitForLoadAsync(page);

        // Re-read the URL after the load wait — the navigation may have finished.
        url = GetPageUrl(page);

        // ۱. Directly on the portal tp.tax.gov.ir — authenticated.
        if (IsUrlOnPortal(url))
        {
            return true;
        }

        // ۲. On the SSO dashboard my.tax.gov.ir: the OTP success often parks the user on
        // the authenticated dashboard instead of redirecting to tp.tax.gov.ir. Detect the
        // dashboard by its distinctive post-login route (/Page/, incl. /Page/Dashboard)
        // or its post-login text.
        if (url.Contains("my.tax.gov.ir", ignoreCase))
        {
            // The authenticated SSO dashboard route (from valid cookies / an already
            // established session). Explicitly includes my.tax.gov.ir/Page/Dashboard.
            if (url.Contains("/Page/", ignoreCase))
            {
                return true;
            }

            var bodyText = await SafePageTextAsync(page);
            if (bodyText.Contains("ورود به کارپوشه تجاری", StringComparison.Ordinal)
                || bodyText.Contains("کارپوشه", StringComparison.Ordinal)
                || bodyText.Contains("خروج", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Best-effort: wait a short moment for a still-loading page DOM before judging the
    /// login state. The wait only triggers while an in-flight navigation is present; when
    /// the DOM is already loaded it returns immediately. Never throws.
    /// </summary>
    private static async Task SafeWaitForLoadAsync(IPage page)
    {
        try
        {
            var state = page.IsClosed ? string.Empty : page.Url;
            if (string.IsNullOrEmpty(state))
            {
                return;
            }

            try
            {
                await page.WaitForLoadStateAsync(
                    LoadState.DOMContentLoaded,
                    new PageWaitForLoadStateOptions { Timeout = 4000 });
            }
            catch (TimeoutException) { /* DOM still loading — proceed with current snapshot */ }
            catch { /* best-effort */ }
        }
        catch { /* page may be closed/disposed — best-effort */ }
    }

    /// <summary>Wait up to 30s for the SSO redirect onto the authenticated area — either
    /// straight to tp.tax.gov.ir or to the logged-in my.tax.gov.ir dashboard.</summary>
    private static async Task<bool> WaitForPortalRedirectAsync(IPage page, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(OtpRedirectTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsLoggedInOrOnPortalAsync(page))
            {
                return true;
            }
            await Task.Delay(400, ct);
        }
        return false;
    }

    /// <summary>
    /// The tax portal keeps the user on the authenticated my.tax.gov.ir dashboard after
    /// OTP confirmation. Click the «ورود به کارپوشه تجاری» control so the session truly
    /// reaches the business dashboard (tp.tax.gov.ir) before we drive an explicit
    /// navigation. Best-effort: never throws.
    /// </summary>
    private static async Task TryOpenBusinessDashboardAsync(IPage page, CancellationToken ct)
    {
        var selectors = new[]
        {
            "a:has-text('ورود به کارپوشه تجاری')",
            "button:has-text('ورود به کارپوشه تجاری')",
            "a:has-text('کارپوشه تجاری')",
            "button:has-text('کارپوشه تجاری')",
            "a:has-text('ورود به کارپوشه')",
            "button:has-text('ورود به کارپوشه')",
            "a:has-text('کارپوشه')",
            "button:has-text('کارپوشه')"
        };

        foreach (var selector in selectors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var handle = await page.QuerySelectorAsync(selector);
                if (handle is null)
                {
                    continue;
                }

                try
                {
                    if (!await handle.IsVisibleAsync())
                    {
                        continue;
                    }

                    await handle.ClickAsync(new ElementHandleClickOptions { Timeout = 6000 });
                    // Give the redirect to the business dashboard a moment to resolve.
                    await Task.Delay(1200, ct);
                    return;
                }
                finally
                {
                    await handle.DisposeAsync();
                }
            }
            catch { /* try the next candidate */ }
        }
    }

    /// <summary>Returns the first visible element matching any selector, or null.</summary>
    private static async Task<IElementHandle?> FirstVisibleAsync(IPage page, IReadOnlyList<string> selectors)
    {
        foreach (var selector in selectors)
        {
            try
            {
                var handle = await page.QuerySelectorAsync(selector);
                if (handle is null) continue;
                var visible = await handle.IsVisibleAsync();
                if (visible) return handle;
                await handle.DisposeAsync();
            }
            catch { /* next selector */ }
        }
        return null;
    }

    /// <summary>
    /// Robustly locate the SMS-OTP entry input on <paramref name="page"/>. First tries the
    /// known CSS <see cref="OtpFieldSelectors"/>; if none match, it falls back to scanning the
    /// live DOM for any <c>&lt;input&gt;</c> whose id/name/class/placeholder/autocomplete looks
    /// like an OTP box — matched case-insensitively and glyph-normalized (so Arabic yeh/kaf and
    /// ZWNJ differences never break matching, mirroring login.py's fill_by_hint). Only a visible
    /// field is returned.
    /// </summary>
    private static async Task<IElementHandle?> FindOtpFieldAsync(IPage page)
    {
        var fast = await FirstVisibleAsync(page, OtpFieldSelectors);
        if (fast is not null)
        {
            return fast;
        }

        try
        {
            var inputs = await page.QuerySelectorAllAsync("input");
            foreach (var input in inputs)
            {
                try
                {
                    if (!await input.IsVisibleAsync())
                    {
                        await input.DisposeAsync();
                        continue;
                    }

                    var id = await input.GetAttributeAsync("id");
                    var name = await input.GetAttributeAsync("name");
                    var cls = await input.GetAttributeAsync("class");
                    var placeholder = await input.GetAttributeAsync("placeholder");
                    var auto = await input.GetAttributeAsync("autocomplete");
                    var inputMode = await input.GetAttributeAsync("inputmode");

                    var idN = PersianTextNormalizer.Normalize(id);
                    var nameN = PersianTextNormalizer.Normalize(name);
                    var clsN = PersianTextNormalizer.Normalize(cls);
                    var phN = PersianTextNormalizer.Normalize(placeholder);
                    var autoN = PersianTextNormalizer.Normalize(auto);
                    var modeN = PersianTextNormalizer.Normalize(inputMode);

                    bool looksOtp =
                        (!string.IsNullOrEmpty(autoN) && autoN.Equals("one-time-code", StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrEmpty(modeN) && modeN.Equals("numeric", StringComparison.OrdinalIgnoreCase))
                        || (ContainsIgnoreCase(phN, "پیامک") || ContainsIgnoreCase(phN, "کد تأیید") || ContainsIgnoreCase(phN, "کد تایید"))
                        || ContainsIgnoreCase(idN, "otp") || ContainsIgnoreCase(nameN, "otp")
                        || ContainsIgnoreCase(clsN, "codeconfirm") || clsN.Contains("CodeConfirm", StringComparison.OrdinalIgnoreCase)
                        || (ContainsIgnoreCase(nameN, "verify") && ContainsIgnoreCase(nameN, "code"));

                    if (looksOtp)
                    {
                        return input;
                    }

                    await input.DisposeAsync();
                }
                catch
                {
                    await input.DisposeAsync();
                }
            }
        }
        catch
        {
            /* DOM scan failed — caller handles a null result */
        }

        return null;
    }

    private static bool ContainsIgnoreCase(string? haystack, string needle)
        => !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Find the page in <paramref name="context"/> that currently shows a visible OTP field, or
    /// null when none does. Handles the persistent-profile multi-tab case deterministically: an
    /// OTP box is stronger evidence of the right tab than any URL guess.
    /// </summary>
    private static async Task<IPage?> FindPageWithOtpFieldAsync(IBrowserContext context, CancellationToken ct)
    {
        foreach (var candidate in context.Pages)
        {
            ct.ThrowIfCancellationRequested();
            var handle = await FindOtpFieldAsync(candidate);
            if (handle is not null)
            {
                await handle.DisposeAsync();
                return candidate;
            }
        }
        return null;
    }

    /// <summary>Locate the درخواست فایل خروجی trigger and open the export modal if not open.</summary>
    private async Task EnsureExportModalAsync(IPage page, CancellationToken ct)
    {
        var existingModal = await FindModalAsync(page);
        if (existingModal is not null)
        {
            var text = await SafeElementTextAsync(existingModal);
            await existingModal.DisposeAsync();
            if (PersianTextNormalizer.NormalizeText(text).Contains(PersianTextNormalizer.NormalizeText(RequestOutputButton)))
            {
                return;
            }
        }

        var clicked = await ClickByTextAsync(page,
            new[] { "button", "input[type='submit']", "a", ".btn-success" }, RequestOutputButton, ct);
        if (!clicked)
        {
            throw new InvalidOperationException("دکمه «درخواست فایل خروجی» پیدا نشد یا قابل کلیک نبود.");
        }

        await WaitUntilModalAsync(page, TimeSpan.FromSeconds(10), ct);
    }

    /// <summary>Select the «خرید داخلی» radio within the export modal.</summary>
    private async Task SelectDomesticPurchaseRadioAsync(IPage page, CancellationToken ct)
    {
        var modal = await FindModalAsync(page);
        try
        {
            var candidates = await QueryAllAsync(page, modal, "label, input[type='radio'], [role='radio']");
            foreach (var candidate in candidates)
            {
                try
                {
                    var text = await SafeElementTextAsync(candidate);
                    if (!string.IsNullOrWhiteSpace(text)
                        && PersianTextNormalizer.NormalizeText(text).Contains(PersianTextNormalizer.NormalizeText(DomesticPurchase)))
                    {
                        var input = await candidate.QuerySelectorAsync("input[type='radio']");
                        if (input is not null)
                        {
                            await input.CheckAsync(new ElementHandleCheckOptions { Force = true, Timeout = 5000 });
                            await input.DisposeAsync();
                        }
                        else
                        {
                            await candidate.ClickAsync(new ElementHandleClickOptions { Timeout = 5000 });
                        }
                        return;
                    }
                }
                finally
                {
                    await candidate.DisposeAsync();
                }
            }
        }
        finally
        {
            if (modal is not null) await modal.DisposeAsync();
        }
    }

    /// <summary>Select ALL visible checkboxes on the export modal (invoice statuses &amp; patterns).</summary>
    private async Task SelectAllCheckboxesAsync(IPage page, CancellationToken ct)
    {
        var modal = await FindModalAsync(page);
        try
        {
            var checkboxes = await QueryAllAsync(page, modal, "input[type='checkbox']");
            foreach (var checkbox in checkboxes)
            {
                try
                {
                    if (!await checkbox.IsVisibleAsync())
                    {
                        continue;
                    }
                    await checkbox.CheckAsync(new ElementHandleCheckOptions { Force = true, Timeout = 4000 });
                }
                catch { /* skip uncheckable */ }
                finally
                {
                    await checkbox.DisposeAsync();
                }
            }
        }
        finally
        {
            if (modal is not null) await modal.DisposeAsync();
        }
    }

    /// <summary>Query elements scoped to the modal root when available, else the page.</summary>
    private static async Task<IReadOnlyList<IElementHandle>> QueryAllAsync(
        IPage page, IElementHandle? modal, string selector)
    {
        return modal is not null
            ? await modal.QuerySelectorAllAsync(selector)
            : await page.QuerySelectorAllAsync(selector);
    }

    /// <summary>Set the fiscal year and period dropdowns on the export modal.</summary>
    private async Task SetTaxYearAndPeriodAsync(IPage page, int year, int period, CancellationToken ct)
    {
        var modal = await FindModalAsync(page);
        try
        {
            await TrySetDropdownValueAsync(page, modal, year.ToString(CultureInfo.InvariantCulture), ct);

            if (PeriodLabels.TryGetValue(period, out var periodLabel))
            {
                await TrySetDropdownValueAsync(page, modal, periodLabel, ct);
            }
        }
        finally
        {
            if (modal is not null) await modal.DisposeAsync();
        }
    }

    /// <summary>Try to set a dropdown/combo value by matching option text (glyph-insensitive).</summary>
    private static async Task<bool> TrySetDropdownValueAsync(
        IPage page, IElementHandle? modal, string valueText, CancellationToken ct)
    {
        foreach (var selector in new[] { "[role='combobox']", "select", ".k-dropdown", "div[class*='select']" })
        {
            IElementHandle? trigger = null;
            try
            {
                trigger = modal is not null ? await modal.QuerySelectorAsync(selector) : await page.QuerySelectorAsync(selector);
                if (trigger is null) continue;

                if (selector == "select")
                {
                    var options = await QueryAllAsync(page, modal, "option");
                    foreach (var option in options)
                    {
                        var text = await SafeElementTextAsync(option);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            text = await option.GetAttributeAsync("value") ?? string.Empty;
                        }
                        if (PersianTextNormalizer.NormalizeText(text).Contains(PersianTextNormalizer.NormalizeText(valueText)))
                        {
                            var value = await option.GetAttributeAsync("value");
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                await trigger.SelectOptionAsync(new[] { value });
                            }
                            await option.DisposeAsync();
                            return true;
                        }
                        await option.DisposeAsync();
                    }
                }
                else
                {
                    await trigger.ClickAsync(new ElementHandleClickOptions { Timeout = 4000 });
                    await Task.Delay(400, ct);
                    var items = await QueryAllAsync(page, modal, "li, [role='option'], .k-item, .k-list-item");
                    foreach (var item in items)
                    {
                        var text = await SafeElementTextAsync(item);
                        if (PersianTextNormalizer.NormalizeText(text).Contains(PersianTextNormalizer.NormalizeText(valueText)))
                        {
                            await item.ClickAsync(new ElementHandleClickOptions { Timeout = 4000 });
                            await item.DisposeAsync();
                            return true;
                        }
                        await item.DisposeAsync();
                    }
                }
            }
            catch { /* try next selector */ }
            finally
            {
                if (trigger is not null) await trigger.DisposeAsync();
            }
        }

        return false;
    }

    /// <summary>Click the green submit button (درخواست فایل خروجی) inside the modal.</summary>
    private async Task ClickSubmitExportRequestAsync(IPage page, CancellationToken ct)
    {
        var modal = await FindModalAsync(page);
        try
        {
            var clicked = await ClickByTextAsync(page,
                new[] { "button", "input[type='submit']", "a" }, RequestOutputButton, ct);
            if (!clicked)
            {
                throw new InvalidOperationException("دکمه ارسال درخواست فایل خروجی داخل مودال پیدا نشد.");
            }

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded,
                new PageWaitForLoadStateOptions { Timeout = 30000 });
        }
        finally
        {
            if (modal is not null) await modal.DisposeAsync();
        }
    }

    /// <summary>Poll the 1st table row every 10s (up to 5 minutes) until the status is «موفق».</summary>
    private async Task PollUntilSuccessAsync(IPage page, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(PollDeadlineSeconds);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            }
            catch { /* ignore reload failures, keep polling */ }

            var topRow = await GetTopRowTextAsync(page);
            if (PersianTextNormalizer.NormalizeText(topRow).Contains(PersianTextNormalizer.NormalizeText(SuccessStatus)))
            {
                _logger.LogInformation("Export queue status is «موفق» (Successful); proceeding to download.");
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), ct);
        }

        throw new TimeoutException("زمان‌بندی تولید فایل خروجی به پایان رسید؛ لطفاً بعداً دوباره تلاش کنید.");
    }

    /// <summary>Trigger and capture the download with ExpectDownloadAsync, saving to the output dir.</summary>
    private async Task<string> DownloadExportFileAsync(IPage page, CancellationToken ct)
    {
        var outDir = _lastOutDir;
        Directory.CreateDirectory(outDir);

        var downloadTask = page.WaitForDownloadAsync(new PageWaitForDownloadOptions { Timeout = 60000 });

        var downloadLink = await FindDownloadLinkAsync(page, ct)
                           ?? throw new InvalidOperationException("دکمه دانلود فایل خروجی روی ردیف بالای جدول پیدا نشد.");
        try
        {
            await downloadLink.ClickAsync(new ElementHandleClickOptions { Timeout = 15000 });
        }
        finally
        {
            await downloadLink.DisposeAsync();
        }

        var download = await downloadTask;
        var suggested = download.SuggestedFilename;
        if (string.IsNullOrWhiteSpace(suggested))
        {
            suggested = $"mowadiyan_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.xlsx";
        }

        var dest = Path.Combine(outDir, SanitizeFileName(suggested));
        await download.SaveAsAsync(dest);
        _logger.LogInformation("Downloaded export file saved at {Path}.", dest);
        return dest;
    }

    /// <summary>Find the export modal root element on the page, if open.</summary>
    private static async Task<IElementHandle?> FindModalAsync(IPage page)
    {
        foreach (var selector in new[] { "div[role='dialog']", ".modal", ".modal-content", ".RadWindow", "[class*='dialog']" })
        {
            try
            {
                var modal = await page.QuerySelectorAsync(selector);
                if (modal is null) continue;
                if (await modal.IsVisibleAsync())
                {
                    return modal;
                }
                await modal.DisposeAsync();
            }
            catch { /* next */ }
        }
        return null;
    }

    /// <summary>Wait until an export modal (containing the request text) becomes visible.</summary>
    private static async Task WaitUntilModalAsync(IPage page, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var modal = await FindModalAsync(page);
            if (modal is not null)
            {
                var text = await SafeElementTextAsync(modal);
                await modal.DisposeAsync();
                if (Body.HasModalKeywords(text))
                {
                    return;
                }
            }
            await Task.Delay(400, ct);
        }
    }

    /// <summary>Click the first visible element whose normalized text contains the target
    /// Persian label. Glyph-insensitive via PersianTextNormalizer.</summary>
    private static async Task<bool> ClickByTextAsync(
        IPage page, IReadOnlyList<string> selectors, string text, CancellationToken ct)
    {
        var needle = PersianTextNormalizer.NormalizeText(text);
        foreach (var tag in selectors)
        {
            var elements = await page.QuerySelectorAllAsync(tag);
            foreach (var element in elements)
            {
                try
                {
                    if (!await element.IsVisibleAsync())
                    {
                        continue;
                    }

                    var inner = await SafeElementTextAsync(element);
                    if (!string.IsNullOrWhiteSpace(inner)
                        && PersianTextNormalizer.NormalizeText(inner).Contains(needle))
                    {
                        await element.ClickAsync(new ElementHandleClickOptions { Timeout = 6000 });
                        return true;
                    }
                }
                catch { /* next */ }
                finally
                {
                    await element.DisposeAsync();
                }
            }
        }

        return false;
    }

    /// <summary>Get the inner text of the first table row on the requests table.</summary>
    private static async Task<string> GetTopRowTextAsync(IPage page)
    {
        foreach (var selector in new[] { "table tbody tr", "table tr", "[class*='grid'] tbody tr", "[class*='RadGrid'] tbody tr" })
        {
            try
            {
                var rows = await page.QuerySelectorAllAsync(selector);
                if (rows.Count > 0)
                {
                    var row = rows[0];
                    try
                    {
                        return await SafeElementTextAsync(row) ?? string.Empty;
                    }
                    finally
                    {
                        await row.DisposeAsync();
                    }
                }
            }
            catch { /* next selector */ }
        }
        return string.Empty;
    }

    /// <summary>Find the download control on the top table row, falling back to a page-wide scan.</summary>
    private static async Task<IElementHandle?> FindDownloadLinkAsync(IPage page, CancellationToken ct)
    {
        foreach (var selector in new[] { "table tbody tr", "table tr", "[class*='grid'] tbody tr" })
        {
            try
            {
                var rows = await page.QuerySelectorAllAsync(selector);
                if (rows.Count == 0)
                {
                    continue;
                }

                var top = rows[0];
                try
                {
                    var link = await FindDownloadLinkInTopRowAsync(top);
                    if (link is not null) return link;
                }
                finally
                {
                    await top.DisposeAsync();
                }
            }
            catch { /* next */ }
        }

        return await FindDownloadLinkInPageAsync(page);
    }

    /// <summary>Scan a single row element for a recognizable download control.</summary>
    private static async Task<IElementHandle?> FindDownloadLinkInTopRowAsync(IElementHandle row)
    {
        foreach (var tag in new[] { "a", "button", "input[type='button']" })
        {
            var elements = await row.QuerySelectorAllAsync(tag);
            foreach (var element in elements)
            {
                try
                {
                    if (await IsDownloadLink(element))
                    {
                        return element;
                    }
                }
                finally
                {
                    await element.DisposeAsync();
                }
            }
        }
        return null;
    }

    private static async Task<IElementHandle?> FindDownloadLinkInPageAsync(IPage page)
    {
        foreach (var tag in new[] { "a", "button", "input[type='button']" })
        {
            var elements = await page.QuerySelectorAllAsync(tag);
            foreach (var element in elements)
            {
                try
                {
                    if (await IsDownloadLink(element))
                    {
                        return element;
                    }
                }
                finally
                {
                    await element.DisposeAsync();
                }
            }
        }
        return null;
    }

    /// <summary>True when an element's text/class/title indicates a download control.</summary>
    private static async Task<bool> IsDownloadLink(IElementHandle element)
    {
        var text = await SafeElementTextAsync(element);
        var cls = (await element.GetAttributeAsync("class") ?? string.Empty).ToLowerInvariant();
        var title = await element.GetAttributeAsync("title") ?? string.Empty;

        return text.ContainsAny("دانلود", "download")
            || cls.Contains("download", StringComparison.Ordinal)
            || cls.Contains("csv", StringComparison.Ordinal)
            || cls.Contains("file", StringComparison.Ordinal)
            || title.ContainsAny("دانلود", "download");
    }

    /// <summary>Best-effort extraction of the exact error text currently visible on the page.</summary>
    private static async Task<string> TryExtractPageErrorAsync(IPage page, CancellationToken ct)
    {
        // The OTP rejection message from /Auth/Confirm is rendered into a <p class="res">
        // (or a .msgError element) on the SSO page — neither is a toast/alert, so include
        // them explicitly so the operator sees the portal's exact reason.
        var selectors = new[] { ".res", ".msgError", ".swal2-content", ".swal2-title", "[class*='swal']",
                                "[role='alert']", ".alert-danger", "[class*='error']", "[class*='toast']",
                                "[class*='message']", ".text-danger" };
        foreach (var selector in selectors)
        {
            try
            {
                var elements = await page.QuerySelectorAllAsync(selector);
                foreach (var el in elements)
                {
                    try
                    {
                        if (!await el.IsVisibleAsync())
                        {
                            continue;
                        }

                        var text = await SafeElementTextAsync(el);
                        if (!string.IsNullOrWhiteSpace(text) && text.Length is >= 2 and <= 500)
                        {
                            return text.Trim();
                        }
                    }
                    finally
                    {
                        await el.DisposeAsync();
                    }
                }
            }
            catch { /* next */ }
        }

        var body = await SafePageTextAsync(page);
        return Body.ErrorMarker(body);
    }

    /// <summary>Best-effort read of the masked phone shown on the OTP screen.</summary>
    private static async Task<string?> TryExtractMaskedPhoneAsync(IPage page, CancellationToken ct)
    {
        try
        {
            var body = await SafePageTextAsync(page);
            var idx = body.IndexOf('*');
            if (idx >= 0)
            {
                var start = Math.Max(0, idx - 3);
                return body.Substring(start, Math.Min(body.Length - start, 18)).Trim();
            }
        }
        catch { /* ignore */ }

        return null;
    }

    /// <summary>
    /// Capture a best-effort debug snapshot of the OTP/verification screen right after
    /// the SMS is sent: saves a full-page screenshot into the output directory
    /// (wwwroot/uploads/mowadiyan) and logs the page's body text so that even without a
    /// readable image the operator can inspect what the portal actually rendered.
    /// Never throws — failures are logged and swallowed.
    /// </summary>
    private async Task CaptureOtpDebugSnapshotAsync(IPage page, string outputDir, CancellationToken ct)
    {
        try
        {
            var screenshotDir = outputDir;
            Directory.CreateDirectory(screenshotDir);
            var name = $"debug_otp_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fff}.png";
            var path = Path.Combine(screenshotDir, name);
            await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Path = path });
            _logger.LogInformation("OTP debug screenshot saved -> {Path}", path);

            var bodyText = await SafePageTextAsync(page);
            var pageState = string.IsNullOrWhiteSpace(bodyText)
                ? "(بدون متن صفحه – صفحه خالی یا JS هنوز رندر نکرده)"
                : bodyText;
            _logger.LogInformation("OTP screen state (page body) at SMS-sent moment: {State}", pageState);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WARNING: could not capture OTP debug snapshot: {Message}", ex.Message);
        }
    }


    /// <summary>Read the page's body inner text without throwing.</summary>
    private static async Task<string> SafePageTextAsync(IPage page)
    {
        try { return await page.InnerTextAsync("body"); }
        catch
        {
            try { return await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 1000 }); }
            catch { return string.Empty; }
        }
    }

    /// <summary>Read an element's inner text without throwing.</summary>
    private static async Task<string> SafeElementTextAsync(IElementHandle element)
    {
        try
        {
            var text = await element.InnerTextAsync();
            return text ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string GetPageUrl(IPage page)
    {
        try { return page.Url ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static bool IsUrlOnPortal(string url)
        => url.Contains(PortalBase, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the URL belongs to either the SSO domain (my.tax.gov.ir) or the
    /// authenticated portal (tp.tax.gov.ir). All other URLs (about:blank, extensions,
    /// unrelated sites) return false.</summary>
    private static bool IsOnSsoOrPortalDomain(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }
        url = url.ToLowerInvariant();
        return url.Contains(PortalBase, StringComparison.OrdinalIgnoreCase)
               || url.Contains(LoginBase, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Pick the most relevant open tab for auth/completion. The persistent profile may
    /// restore several tabs (including a stray about:blank); we must drive the tab that
    /// actually shows the OTP screen or the authenticated portal, not the first one.
    /// Priority: (1) authenticated portal tab, (2) a Tax-domain (SSO) tab with a visible
    /// OTP input / OTP page text, (3) any Tax-domain tab, (4) fall back to the first tab.
    /// </summary>
    private static async Task<IPage?> SelectSessionPageAsync(IBrowserContext context, CancellationToken ct)
    {
        var pages = context.Pages;
        if (pages.Count == 0)
        {
            return null;
        }

        IPage? portalTab = null;
        IPage? otpTab = null;
        IPage? taxDomainTab = null;

        foreach (var candidate in pages)
        {
            ct.ThrowIfCancellationRequested();
            var url = GetPageUrl(candidate);

            if (IsUrlOnPortal(url))
            {
                portalTab ??= candidate;
            }
            else if (IsOnSsoOrPortalDomain(url))
            {
                taxDomainTab ??= candidate;
                // Heuristic: an open OTP screen is our best signal we selected the right tab.
                if (otpTab is null && await IsOtpInputVisibleAsync(candidate))
                {
                    otpTab = candidate;
                }
            }
        }

        return portalTab ?? otpTab ?? taxDomainTab ?? pages.FirstOrDefault();
    }

    /// <summary>Resolve the fixed persistent-profile directory (./tax_browser_profile).</summary>
    private static string ResolveProfileDir()
        => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tax_browser_profile"));

    private static string ResolveDefaultOutputDir()
        => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "mowadiyan");

    private static async Task<IPlaywright> PreparePlaywrightAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return await Playwright.CreateAsync();
    }

    private IBrowserContext? ResolveContext(string token, out bool ownsContext)
    {
        ownsContext = false;
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (_sessions.TryGetValue(token, out var session))
        {
            ownsContext = true;
            return session.Context;
        }

        return null;
    }

    private static async Task DisposeSessionContextAsync(IBrowserContext? context, IPlaywright? playwright)
    {
        try
        {
            if (context is not null)
            {
                await context.CloseAsync();
            }
        }
        catch { /* best-effort */ }

        try
        {
            playwright?.Dispose();
        }
        catch { /* best-effort */ }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }
        return fileName;
    }
}

/// <summary>Keyword classification helpers for the Tax Portal page text.</summary>
internal static class Body
{
    private static readonly string[] OtpKeywords =
    {
        "کد تأیید", "کد تایید", "رمز یکبار مصرف", "کد ارسال شده", "کد پیامک"
    };

    private static readonly string[] ErrorMarkers =
    {
        "تصویر امنیتی اشتباه است", "تصویر امنیتی نادرست است",
        "نام کاربری یا کلمه عبور نادرست است", "کد امنیتی اشتباه است",
        "کد امنیتی معتبر نیست", "کد ملی یا شماره همراه ثبت نشده", "کد پیامک اشتباه است",
        // OTP-rejection markers (mirrors login.py OTP_ERROR_MARKERS) so the exact reason
        // the SMS code was refused is surfaced to the operator.
        "کد تأیید اشتباه است", "کد تایید اشتباه است", "کد وارد شده صحیح نمی‌باشد",
        "کد وارد شده صحیح نیست", "کد تأیید نامعتبر است", "کد پیامک نامعتبر است",
        "مهلت استفاده از کد تأیید به پایان رسیده", "کد تأیید منقضی شده", "احراز هویت ناموفق"
    };

    private static readonly string[] ModalKeywords = { "درخواست فایل خروجی", "خرید", "سال صدور" };

    public static bool HasOtpKeywords(string text)
        => !string.IsNullOrEmpty(text) && text.ContainsAny(OtpKeywords);

    public static bool HasErrorMarkers(string text)
        => !string.IsNullOrEmpty(text) && text.ContainsAny(ErrorMarkers);

    public static bool HasModalKeywords(string text)
        => !string.IsNullOrEmpty(text) && text.ContainsAny(ModalKeywords);

    public static string ErrorMarker(string text)
    {
        foreach (var marker in ErrorMarkers)
        {
            if (!string.IsNullOrEmpty(text) && text.Contains(marker, StringComparison.Ordinal))
            {
                return marker;
            }
        }
        return string.Empty;
    }
}

/// <summary>Small string utilities used across the exporter.</summary>
internal static class TaxExporterTextExtensions
{
    /// <summary>True when <paramref name="source"/> contains any of the candidates (Ordinal).</summary>
    public static bool ContainsAny(this string source, params string[] candidates)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) && source.Contains(candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

