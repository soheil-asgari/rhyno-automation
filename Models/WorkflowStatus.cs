namespace OfficeAutomation.Models
{
    public static class WorkflowStatus
    {
        public const string Draft = "Draft";
        public const string Hold = "Hold";
        public const string Sent = "Sent";
        public const string PendingApproval = "PendingApproval";
        public const string PendingManager = "PendingManager";
        public const string NeedsRevision = "NeedsRevision";
        public const string Rejected = "Rejected";
        public const string Approved = "Approved";
        public const string Archived = "Archived";
        public const string Canceled = "Canceled";
        public const string Completed = "Completed";
        public const string Returned = "Returned";
        public const string Paused = "Paused";
        public const string Incident = "Incident";

        public static readonly string[] All =
        [
            Draft,
            Hold,
            Sent,
            PendingApproval,
            PendingManager,
            NeedsRevision,
            Rejected,
            Approved,
            Archived,
            Canceled,
            Completed,
            Returned,
            Paused,
            Incident
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
                "نیاز به اصلاح" or "NeedsRevision" => NeedsRevision,
                "رد شده" or "ردشده" or "Rejected" => Rejected,
                "تایید شده" or "تأیید شده" or "تاییدشده" or "Approved" or "Finalized" => Approved,
                "بایگانی شده" or "بایگانی‌شده" or "Archived" => Archived,
                "ابطال شده" or "Canceled" => Canceled,
                "انجام شده" or "Completed" => Completed,
                "برگشت داده شده" or "Returned" => Returned,
                "متوقف شده" or "Paused" => Paused,
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
                NeedsRevision => "نیاز به اصلاح",
                Rejected => "رد شده",
                Approved => "تایید شده",
                Archived => "بایگانی شده",
                Canceled => "ابطال شده",
                Completed => "انجام شده",
                Returned => "برگشت داده شده",
                Paused => "متوقف شده",
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
                NeedsRevision => "text-bg-info",
                Rejected => "text-bg-danger",
                Approved => "text-bg-success",
                Archived => "text-bg-dark",
                Canceled => "text-bg-dark",
                Completed => "text-bg-success",
                Returned => "text-bg-secondary",
                Paused => "text-bg-dark",
                _ => "text-bg-light"
            };
        }

        public static bool IsPending(string? status) => Normalize(status) == PendingApproval;

        public static bool IsActionPending(string? status)
        {
            var normalized = Normalize(status);
            return normalized == PendingApproval || normalized == PendingManager || normalized == NeedsRevision || normalized == Returned;
        }

        public static bool IsApproved(string? status) => Normalize(status) == Approved;

        public static bool IsRejected(string? status) => Normalize(status) == Rejected;
    }
}
