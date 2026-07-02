namespace OfficeAutomation.Models;

public static class WorkflowAssignmentMode
{
    public const string User = "User";
    public const string Role = "Role";
    public const string Department = "Department";
    public const string Dynamic = "Dynamic";
}

public static class WorkflowDecisionType
{
    public const string Approve = "Approve";
    public const string Reject = "Reject";
    public const string Return = "Return";
    public const string RequestChanges = "RequestChanges";
    public const string Forward = "Forward";
    public const string Delegate = "Delegate";
    public const string Comment = "Comment";
    public const string Start = "Start";
    public const string Escalate = "Escalate";
    public const string Pause = "Pause";
    public const string Resume = "Resume";
    public const string AdHocAssign = "AdHocAssign";
    public const string CreateSubCase = "CreateSubCase";
    public const string CompleteSubCase = "CompleteSubCase";
}

public static class WorkflowSlaState
{
    public const string OnTrack = "OnTrack";
    public const string DueSoon = "DueSoon";
    public const string Overdue = "Overdue";
    public const string Breached = "Breached";
}
