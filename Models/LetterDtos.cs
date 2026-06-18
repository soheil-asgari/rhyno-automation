using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public sealed class LetterCreateDto
{
    [Required(ErrorMessage = "گیرنده الزامی است.")]
    public string ReceiverId { get; set; } = string.Empty;

    [Required(ErrorMessage = "موضوع الزامی است.")]
    [StringLength(200, ErrorMessage = "موضوع نمی‌تواند بیش از ۲۰۰ کاراکتر باشد.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "متن نامه الزامی است.")]
    public string Body { get; set; } = string.Empty;

    public int? ReplyToLetterId { get; set; }

    public string? Priority { get; set; }

    public string? Classification { get; set; }

    public DateTimeOffset? FollowUpDate { get; set; }

    public bool RequiresFollowUp { get; set; }

    public string[]? CopyRecipientIds { get; set; }
}

public sealed class LetterDraftRequest
{
    public string? ReceiverId { get; set; }
    public string? Subject { get; set; }
    public string? Instruction { get; set; }
    public string? CurrentBody { get; set; }
}

public sealed class LetterSummaryRequest
{
    public string? Mode { get; set; }
    public int? LetterId { get; set; }
}

public sealed class LetterReplyRequest
{
    public int LetterId { get; set; }
    public string? Intent { get; set; }
}

public sealed class LetterNoteDto
{
    [Required]
    public string Content { get; set; } = string.Empty;
    public bool IsManagersOnly { get; set; }
}

public sealed class LetterForwardDto
{
    [Required(ErrorMessage = "کاربر مقصد الزامی است.")]
    public string ForwardToUserId { get; set; } = string.Empty;
}
