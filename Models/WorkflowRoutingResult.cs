namespace OfficeAutomation.Models;

public sealed class WorkflowRoutingResult
{
    public string ReceiverId { get; init; } = string.Empty;
    public string Status { get; init; } = WorkflowStatus.PendingApproval;
    public int StepNumber { get; init; }
    public string? StepKey { get; init; }
    public bool IsCompleted { get; init; }

    public static WorkflowRoutingResult Pending(string receiverId, int stepNumber, string? stepKey = null)
    {
        return new WorkflowRoutingResult
        {
            ReceiverId = receiverId,
            Status = WorkflowStatus.PendingApproval,
            StepNumber = stepNumber,
            StepKey = stepKey,
            IsCompleted = false
        };
    }

    public static WorkflowRoutingResult Completed(string receiverId, string status, int stepNumber, string? stepKey = null)
    {
        return new WorkflowRoutingResult
        {
            ReceiverId = receiverId,
            Status = WorkflowStatus.Normalize(status),
            StepNumber = stepNumber,
            StepKey = stepKey,
            IsCompleted = true
        };
    }
}
