namespace OfficeAutomation.Models
{
    public static class WorkflowStatus
    {
        public const string Draft = "Draft";
        public const string Sent = "Sent";
        public const string PendingApproval = "PendingApproval";
        public const string Rejected = "Rejected";
        public const string Approved = "Approved";
        public const string Archived = "Archived";

        public static readonly string[] All =
        [
            Draft,
            Sent,
            PendingApproval,
            Rejected,
            Approved,
            Archived
        ];

        public static string Normalize(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return Draft;
            }

            return status.Trim() switch
            {
                "پیش نویس" or "پیشنویس" or "Draft" => Draft,
                "ارسال شده" or "ارسال‌شده" or "Sent" => Sent,
                "در انتظار" or "در انتظار تایید" or "در انتظار تأیید" or "Pending" or "PendingManager" or "PendingApproval" => PendingApproval,
                "رد شده" or "ردشده" or "Rejected" => Rejected,
                "تایید شده" or "تأیید شده" or "تاییدشده" or "Approved" or "Finalized" => Approved,
                "بایگانی شده" or "بایگانی‌شده" or "Archived" => Archived,
                var value => value
            };
        }

        public static string Label(string? status)
        {
            return Normalize(status) switch
            {
                Draft => "پیش نویس",
                Sent => "ارسال شده",
                PendingApproval => "در انتظار تایید",
                Rejected => "رد شده",
                Approved => "تایید شده",
                Archived => "بایگانی شده",
                _ => status ?? "-"
            };
        }

        public static string BadgeCss(string? status)
        {
            return Normalize(status) switch
            {
                Draft => "text-bg-secondary",
                Sent => "text-bg-primary",
                PendingApproval => "text-bg-warning",
                Rejected => "text-bg-danger",
                Approved => "text-bg-success",
                Archived => "text-bg-dark",
                _ => "text-bg-light"
            };
        }

        public static bool IsPending(string? status) => Normalize(status) == PendingApproval;
        public static bool IsApproved(string? status) => Normalize(status) == Approved;
        public static bool IsRejected(string? status) => Normalize(status) == Rejected;
    }
}
