using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OfficeAutomation.Modules.IntakeAutomation;
using OfficeAutomation.Modules.InvoiceIntake;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers;

/// <summary>
/// Secure webhook intake endpoint for external automation connectors.
/// Authenticated via API Key (X-Api-Key header or api_key query parameter).
/// </summary>
[ApiController]
[Route("api/v1/intake")]
[EnableRateLimiting("api")]
public sealed class WebhookIntakeController : ControllerBase
{
    private readonly InvoiceIntakeService _intakeService;
    private readonly ZipArchiveProcessor _zipProcessor;
    private readonly TelegramBotWebhookHandler _telegramHandler;
    private readonly ILogger<WebhookIntakeController> _logger;

    public WebhookIntakeController(
        InvoiceIntakeService intakeService,
        ZipArchiveProcessor zipProcessor,
        TelegramBotWebhookHandler telegramHandler,
        ILogger<WebhookIntakeController> logger)
    {
        _intakeService = intakeService;
        _zipProcessor = zipProcessor;
        _telegramHandler = telegramHandler;
        _logger = logger;
    }

    /// <summary>
    /// Receives a single invoice file (multipart "file" or base64) and queues it for intake.
    /// </summary>
    [HttpPost("webhook")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
    [Produces("application/json")]
    public async Task<IActionResult> Webhook(
        [FromForm] IFormFile? file,
        [FromQuery] string? ownerKey = null,
        CancellationToken cancellationToken = default)
    {
        var owner = ResolveOwnerKey(ownerKey);
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "فایلی برای بارگذاری ارسال نشده است." });
        }

        var item = _intakeService.ValidateAndStore(file, owner, out var error);
        if (item is null)
        {
            _logger.LogWarning("Webhook intake rejected file '{FileName}': {Error}", file.FileName, error);
            return BadRequest(new { success = false, message = error });
        }

        var process = await _intakeService.ProcessAsync(item.Id, owner, cancellationToken);
        _logger.LogInformation("Webhook intake processed file '{FileName}' (success={Success}).", file.FileName, process.Success);

        return Ok(new
        {
            success = process.Success,
            message = process.Message,
            item = process.Item
        });
    }

    /// <summary>
    /// Receives a base64-encoded file body (alternative to multipart).
    /// </summary>
    [HttpPost("webhook/base64")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
    [Produces("application/json")]
    public async Task<IActionResult> WebhookBase64(
        [FromBody] WebhookIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.FileBase64))
        {
            return BadRequest(new { success = false, message = "محتوای base64 فایل ارسال نشده است." });
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.FileBase64);
        }
        catch (FormatException)
        {
            return BadRequest(new { success = false, message = "محتوا به‌درستی base64 نیست." });
        }

        var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "webhook-invoice.pdf" : request.FileName;
        using var stream = new MemoryStream(bytes);
        var formFile = new FormFile(stream, 0, stream.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = ResolveContentType(fileName)
        };

        var owner = ResolveOwnerKey(request.OwnerKey);
        var item = _intakeService.ValidateAndStore(formFile, owner, out var error);
        if (item is null)
        {
            return BadRequest(new { success = false, message = error });
        }

        var process = await _intakeService.ProcessAsync(item.Id, owner, cancellationToken);
        return Ok(new { success = process.Success, message = process.Message, item = process.Item });
    }
/// <summary>
    /// Receives a bulk ZIP archive and auto-queues its supported entries.
    /// </summary>
    [HttpPost("webhook/zip")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
    [Produces("application/json")]
    public async Task<IActionResult> WebhookZip(
        [FromForm] IFormFile? file,
        [FromQuery] string? ownerKey = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "فایل ZIP ارسال نشده است." });
        }

        var owner = ResolveOwnerKey(ownerKey);
        await using var stream = file.OpenReadStream();
        var result = await _zipProcessor.ProcessZipAsync(stream, owner, cancellationToken);
        return result.Success
            ? Ok(new { success = true, stored = result.StoredCount, processed = result.ProcessedCount, rejected = result.RejectedCount, errors = result.Errors })
            : BadRequest(new { success = false, message = result.Message, errors = result.Errors });
    }

    /// <summary>
    /// Receives a Telegram / WhatsApp webhook update and queues any attached media.
    /// </summary>
    [HttpPost("webhook/telegram")]
    [Produces("application/json")]
    public IActionResult TelegramWebhook([FromBody] object payload)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(payload);
        var message = _telegramHandler.Parse(body);
        if (message is null)
        {
            return Ok(new { success = true, handled = false, message = "پیام پشتیبانی‌شده‌ای نبود." });
        }

        _logger.LogInformation(
            "Telegram webhook received from {Sender} with {MediaCount} media attachment(s).",
            message.SenderId, message.MediaFileIds.Count);

        return Ok(new
        {
            success = true,
            handled = true,
            source = message.Source,
            senderId = message.SenderId,
            text = message.Text,
            hasMedia = message.HasMedia,
            mediaCount = message.MediaFileIds.Count
        });
    }

    private string ResolveOwnerKey(string? ownerKey)
    {
        if (!string.IsNullOrWhiteSpace(ownerKey))
        {
            return ownerKey;
        }

        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "api-client";
    }

    private static string ResolveContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".pdf" => "application/pdf",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}