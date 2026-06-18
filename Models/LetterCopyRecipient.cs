using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class LetterCopyRecipient
{
    public int Id { get; set; }

    public int LetterId { get; set; }

    public Letter? Letter { get; set; }

    [Required]
    [StringLength(450)]
    public string RecipientId { get; set; } = string.Empty;

    [Display(Name = "گیرنده رونوشت")]
    public User? Recipient { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public bool IsRead { get; set; }

    public DateTimeOffset? ReadDate { get; set; }
}
