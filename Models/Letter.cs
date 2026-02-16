namespace OfficeAutomation.Models;
using OfficeAutomation.Models;

public class Letter
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty; // اضافه کردن مقدار اولیه
    public string Body { get; set; } = string.Empty;
    public DateTime SentDate { get; set; } = DateTime.Now;

    public string SenderId { get; set; } = string.Empty;
    public User? Sender { get; set; } // علامت سوال برای رفع ارور

    public string ReceiverId { get; set; } = string.Empty;
    public User? Receiver { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime? ReadDate { get; set; }
}