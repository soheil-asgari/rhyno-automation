using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class Letter
{
    public int Id { get; set; }

    [Display(Name = "موضوع نامه")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "متن نامه")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "تاریخ ارسال")]
    public DateTime SentDate { get; set; } = DateTime.Now;

    public string SenderId { get; set; } = string.Empty;

    [Display(Name = "فرستنده")]
    public User? Sender { get; set; }

    public string ReceiverId { get; set; } = string.Empty;

    [Display(Name = "گیرنده")]
    public User? Receiver { get; set; }

    public string? FinalReceiverId { get; set; }

    [Display(Name = "گیرنده نهایی")]
    public User? FinalReceiver { get; set; }

    [Display(Name = "وضعیت خوانده شدن")]
    public bool IsRead { get; set; } = false;

    [Display(Name = "تاریخ مشاهده")]
    public DateTime? ReadDate { get; set; }

    [Display(Name = "نوع سند")]
    public string DocumentType { get; set; } = "Letter";

    [Display(Name = "مرحله گردش کار")]
    public int CurrentWorkflowStep { get; set; }

    [Display(Name = "تکمیل گردش کار")]
    public bool IsWorkflowCompleted { get; set; }
}
