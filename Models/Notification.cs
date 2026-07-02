using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class Notification
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string? TenantId { get; set; }

    [Required]
    [MaxLength(450)]
    public string RecipientUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(600)]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = NotificationSeverity.Info;

    [MaxLength(400)]
    public string? LinkUrl { get; set; }

    [MaxLength(80)]
    public string? SourceModule { get; set; }

    [MaxLength(120)]
    public string? SourceEntityType { get; set; }

    public int? SourceEntityId { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public User? RecipientUser { get; set; }
}

public static class NotificationSeverity
{
    public const string Info = "Info";
    public const string Success = "Success";
    public const string Warning = "Warning";
    public const string Danger = "Danger";

    public static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "success" => Success,
            "warning" => Warning,
            "danger" or "error" => Danger,
            _ => Info
        };
    }

    public static string Icon(string? value)
    {
        return Normalize(value) switch
        {
            Success => "bi-check2-circle",
            Warning => "bi-exclamation-triangle",
            Danger => "bi-x-octagon",
            _ => "bi-info-circle"
        };
    }

    public static string CssTone(string? value)
    {
        return Normalize(value).ToLowerInvariant();
    }
}
