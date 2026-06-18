using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class LetterNote
{
    public int Id { get; set; }

    public int LetterId { get; set; }

    public Letter? Letter { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    [Display(Name = "نویسنده یادداشت")]
    public User? Author { get; set; }

    [Required(ErrorMessage = "متن یادداشت الزامی است.")]
    [Display(Name = "متن یادداشت")]
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    [Display(Name = "فقط برای مدیران")]
    public bool IsManagersOnly { get; set; }
}
