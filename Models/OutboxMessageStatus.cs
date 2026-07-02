namespace OfficeAutomation.Models;

public static class OutboxMessageStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
    public const string DeadLetter = "DeadLetter";
}
