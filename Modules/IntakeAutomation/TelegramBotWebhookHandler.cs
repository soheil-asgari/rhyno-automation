using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OfficeAutomation.Modules.IntakeAutomation;

/// <summary>
/// Lightweight parser for Telegram / WhatsApp webhook payloads. It extracts the
/// message text, sender and any downloadable media (file ids / URLs) so the intake
/// pipeline can later fetch and OCR the attachments. It is intentionally schema-agnostic
/// and tolerant of the many subtle differences between the two channels.
/// </summary>
public sealed class TelegramBotWebhookHandler
{
    private readonly ILogger<TelegramBotWebhookHandler> _logger;

    public TelegramBotWebhookHandler(ILogger<TelegramBotWebhookHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a raw JSON webhook body into a normalized <see cref="IncomingMediaMessage"/>.
    /// Returns null when the payload is not a supported message update.
    /// </summary>
    public IncomingMediaMessage? Parse(string jsonBody, string? forcedSource = null)
    {
        if (string.IsNullOrWhiteSpace(jsonBody))
        {
            _logger.LogDebug("Ignoring empty webhook body.");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            var source = (forcedSource ?? DetectSource(root)).ToLowerInvariant();

            if (source == "telegram")
            {
                return ParseTelegram(root);
            }

            if (source == "whatsapp")
            {
                return ParseWhatsApp(root);
            }

            _logger.LogWarning("Webhook payload source could not be determined.");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Webhook body is not valid JSON.");
            return null;
        }
    }

    private static string DetectSource(JsonElement root)
    {
        // Telegram "update" payloads carry an "update_id" and possible "message".
        if (root.TryGetProperty("update_id", out _))
        {
            return "telegram";
        }

        // WhatsApp Cloud API envelopes carry "entry > changes > value > messages".
        if (root.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Array)
        {
            return "whatsapp";
        }

        return string.Empty;
    }

    private static IncomingMediaMessage ParseTelegram(JsonElement root)
    {
        var message = new IncomingMediaMessage { Source = "telegram" };

        if (root.TryGetProperty("update_id", out var updateId))
        {
            message.MessageId = updateId.ToString();
        }

        if (!root.TryGetProperty("message", out var msg) || msg.ValueKind != JsonValueKind.Object)
        {
            // Channel posts are also accepted.
            msg = root.TryGetProperty("channel_post", out var post) ? post : default;
        }

        if (msg.ValueKind != JsonValueKind.Object)
        {
            return null!;
        }

        if (msg.TryGetProperty("message_id", out var mid))
        {
            message.MessageId = mid.ToString();
        }

        if (msg.TryGetProperty("text", out var text))
        {
            message.Text = text.GetString();
        }
        else if (msg.TryGetProperty("caption", out var caption))
        {
            message.Text = caption.GetString();
        }

        if (msg.TryGetProperty("from", out var from) && from.ValueKind == JsonValueKind.Object)
        {
            if (from.TryGetProperty("id", out var fid))
            {
                message.SenderId = fid.ToString();
            }

            if (from.TryGetProperty("first_name", out var first))
            {
                message.SenderName = first.GetString();
            }

            if (from.TryGetProperty("last_name", out var last) && !string.IsNullOrWhiteSpace(last.GetString()))
            {
                message.SenderName = $"{message.SenderName} {last.GetString()}".Trim();
            }
        }

        // Telegram photo messages expose several sizes; the largest is preferred for OCR.
        if (msg.TryGetProperty("photo", out var photo) && photo.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in photo.EnumerateArray())
            {
                if (item.TryGetProperty("file_id", out var fileId))
                {
                    message.MediaFileIds.Add(fileId.GetString() ?? string.Empty);
                }
            }

            message.MediaContentType = "image/jpeg";
        }
        else if (msg.TryGetProperty("document", out var document) && document.ValueKind == JsonValueKind.Object)
        {
            if (document.TryGetProperty("file_id", out var fileId))
            {
                message.MediaFileIds.Add(fileId.GetString() ?? string.Empty);
            }

            if (document.TryGetProperty("mime_type", out var mime))
            {
                message.MediaContentType = mime.GetString();
            }
        }

        // De-duplicate media ids while preserving order.
        message.MediaFileIds = message.MediaFileIds.Distinct().ToList();
        return message;
    }
private static IncomingMediaMessage ParseWhatsApp(JsonElement root)
    {
        var message = new IncomingMediaMessage { Source = "whatsapp" };

        if (!root.TryGetProperty("entry", out var entry) || entry.ValueKind != JsonValueKind.Array)
        {
            return null!;
        }

        foreach (var entryItem in entry.EnumerateArray())
        {
            if (!entryItem.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var msg in messages.EnumerateArray())
                {
                    if (msg.TryGetProperty("id", out var msgId))
                    {
                        message.MessageId = msgId.GetString();
                    }

                    if (msg.TryGetProperty("from", out var from))
                    {
                        message.SenderId = from.GetString();
                    }

                    if (msg.TryGetProperty("text", out var textEl) && textEl.TryGetProperty("body", out var body))
                    {
                        message.Text = body.GetString();
                    }

                    if (msg.TryGetProperty("caption", out var caption))
                    {
                        message.Text = caption.GetString();
                    }

                    // WhatsApp media messages carry an "image"/"document" object with an id.
                    if (msg.TryGetProperty("image", out var image) && image.TryGetProperty("id", out var imageId))
                    {
                        message.MediaFileIds.Add(imageId.GetString() ?? string.Empty);
                        message.MediaContentType = "image/jpeg";
                    }
                    else if (msg.TryGetProperty("document", out var doc) && doc.TryGetProperty("id", out var docId))
                    {
                        message.MediaFileIds.Add(docId.GetString() ?? string.Empty);
                        if (doc.TryGetProperty("mime_type", out var mime))
                        {
                            message.MediaContentType = mime.GetString();
                        }
                    }
                }
            }
        }

        message.MediaFileIds = message.MediaFileIds.Distinct().ToList();
        return message;
    }
}