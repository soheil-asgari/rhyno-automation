namespace OfficeAutomation.Models
{
    public static class WorkflowStatus
    {
        public const string Draft = "Draft";
        public const string Hold = "Hold";
        public const string Sent = "Sent";
        public const string PendingApproval = "PendingApproval";
        public const string PendingManager = "PendingManager";
        public const string Rejected = "Rejected";
        public const string Approved = "Approved";
        public const string Archived = "Archived";
        public const string Canceled = "Canceled";
        public const string Completed = "Completed";

        public static readonly string[] All =
        [
            Draft,
            Hold,
            Sent,
            PendingApproval,
            PendingManager,
            Rejected,
            Approved,
            Archived,
            Canceled,
            Completed
        ];

        public static string Normalize(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return Draft;
            }

            return status.Trim() switch
            {
                "پیش نویس" or "پیش‌نویس" or "Draft" => Draft,
                "در انتظار" or "Hold" => Hold,
                "ارسال شده" or "ارسال‌شده" or "Sent" => Sent,
                "در انتظار تایید" or "در انتظار تأیید" or "Pending" or "PendingApproval" => PendingApproval,
                "در انتظار مدیر" => PendingManager,
                "رد شده" or "ردشده" or "Rejected" => Rejected,
                "تایید شده" or "تأیید شده" or "تاییدشده" or "Approved" or "Finalized" => Approved,
                "بایگانی شده" or "بایگانی‌شده" or "Archived" => Archived,
                "ابطال شده" or "Canceled" => Canceled,
                "انجام شده" or "Completed" => Completed,
                var value => value
            };
        }

        public static string Label(string? status)
        {
            return Normalize(status) switch
            {
                Draft => "پیش نویس",
                Hold => "در انتظار",
                Sent => "ارسال شده",
                PendingApproval => "در انتظار تایید",
                PendingManager => "در انتظار مدیر",
                Rejected => "رد شده",
                Approved => "تایید شده",
                Archived => "بایگانی شده",
                Canceled => "ابطال شده",
                Completed => "انجام شده",
                _ => status ?? "-"
            };
        }

        public static string BadgeCss(string? status)
        {
            return Normalize(status) switch
            {
                Draft => "text-bg-secondary",
                Hold => "text-bg-info",
                Sent => "text-bg-primary",
                PendingApproval => "text-bg-warning",
                PendingManager => "text-bg-info",
                Rejected => "text-bg-danger",
                Approved => "text-bg-success",
                Archived => "text-bg-dark",
                Canceled => "text-bg-dark",
                Completed => "text-bg-success",
                _ => "text-bg-light"
            };
        }

        public static bool IsPending(string? status) => Normalize(status) == PendingApproval;

        public static bool IsActionPending(string? status)
        {
            var normalized = Normalize(status);
            return normalized == PendingApproval || normalized == PendingManager;
        }

        public static bool IsApproved(string? status) => Normalize(status) == Approved;

        public static bool IsRejected(string? status) => Normalize(status) == Rejected;
    }
}
