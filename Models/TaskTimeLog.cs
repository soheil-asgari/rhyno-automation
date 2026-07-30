using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

/// <summary>
/// ثبت زمان کارکرد روی تسک‌های گردش کار
/// </summary>
public class TaskTimeLog
{
    public int Id { get; set; }

    public int WorkflowCaseTaskId { get; set; }
    public WorkflowCaseTask? WorkflowCaseTask { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    /// <summary>ساعت صرف‌شده</summary>
    [Range(0.01, 24)]
    public decimal HoursSpent { get; set; }

    /// <summary>شرح کار انجام‌شده</summary>
    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>تاریخ ثبت کارکرد</summary>
    public DateTime LoggedAt { get; set; } = DateTime.Now;
}