namespace OfficeAutomation.Models
{
    public class Leave
    {
        public int Id { get; set; }

        // آیدی کاربر در Identity همیشه رشته (string) است
        public string UserId { get; set; } = string.Empty;

        // علامت سوال یعنی این فیلد می‌تواند در ابتدا خالی باشد (برای جلوگیری از خطای Constructor)
        public User? User { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string Reason { get; set; } = string.Empty;

        // تغییر از int به string برای ذخیره "در انتظار"، "تایید شده" و غیره
        public string Status { get; set; } = "در انتظار تایید";
    }
}