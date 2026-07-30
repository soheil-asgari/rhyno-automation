using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.DTOs.Api.v1;

/// <summary>
/// مدل خلاصه تسک در کارتابل
/// </summary>
public class PendingTaskDto
{
    /// <summary>
    /// شناسه یکتای تسک
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// نوع مستند (Invoice, Leave, ...)
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// شناسه مستند
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// عنوان تسک
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// توضیحات
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// وضعیت فعلی گردش کار
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// اولویت (Normal/High/Urgent)
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// شماره گام جاری
    /// </summary>
    public int CurrentStepNumber { get; set; }

    /// <summary>
    /// تاریخ شروع
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// مهلت انجام
    /// </summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>
    /// وضعیت SLA
    /// </summary>
    public string? SlaState { get; set; }

    /// <summary>
    /// نام شروع‌کننده
    /// </summary>
    public string? StartedByUserName { get; set; }

    public static PendingTaskDto From(Models.WorkflowInstance instance) => new()
    {
        Id = instance.Id,
        DocumentType = instance.DocumentType,
        DocumentId = instance.DocumentId,
        Title = $"{instance.DocumentType} #{instance.DocumentId}",
        Description = $"وضعیت: {instance.Status} | گام: {instance.CurrentStepNumber}",
        Status = instance.Status,
        Priority = instance.Priority,
        CurrentStepNumber = instance.CurrentStepNumber,
        StartedAt = instance.StartedAt,
        DueAt = instance.DueAt,
        SlaState = instance.SlaState,
        StartedByUserName = instance.StartedByUser?.FullName ?? instance.StartedByUser?.UserName
    };
}

/// <summary>
/// مدل درخواست تایید یا رد تسک
/// </summary>
public class TaskDecisionRequest
{
    /// <summary>
    /// دلیل تایید یا رد (الزامی برای رد)
    /// </summary>
    [StringLength(1000)]
    public string? Reason { get; set; }

    /// <summary>
    /// توضیحات تکمیلی
    /// </summary>
    [StringLength(2000)]
    public string? Comment { get; set; }
}