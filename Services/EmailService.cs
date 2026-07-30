using System.Net;
using System.Net.Mail;
using System.Text;

namespace OfficeAutomation.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink, CancellationToken cancellationToken = default);
    Task SendTaskAssignmentEmailAsync(string toEmail, string userName, string taskTitle, string assignedByName, string taskUrl, CancellationToken cancellationToken = default);
    Task SendScheduledReportEmailAsync(string toEmail, string userName, string reportTitle, byte[]? attachment, string attachmentFileName, CancellationToken cancellationToken = default);
}

public sealed class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink, CancellationToken cancellationToken = default)
    {
        var subject = "بازیابی رمز عبور - راینو ERP";
        var body = BuildPasswordResetHtml(userName, resetLink);
        await SendEmailAsync(toEmail, subject, body, null, null, cancellationToken);
    }

    public async Task SendTaskAssignmentEmailAsync(string toEmail, string userName, string taskTitle, string assignedByName, string taskUrl, CancellationToken cancellationToken = default)
    {
        var subject = $"تسک جدید: {taskTitle} - راینو ERP";
        var body = BuildTaskAssignmentHtml(userName, taskTitle, assignedByName, taskUrl);
        await SendEmailAsync(toEmail, subject, body, null, null, cancellationToken);
    }

    public async Task SendScheduledReportEmailAsync(string toEmail, string userName, string reportTitle, byte[]? attachment, string attachmentFileName, CancellationToken cancellationToken = default)
    {
        var subject = $"{reportTitle} - راینو ERP";
        var body = BuildScheduledReportHtml(userName, reportTitle);
        await SendEmailAsync(toEmail, subject, body, attachment, attachmentFileName, cancellationToken);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody, byte[]? attachment = null, string? attachmentFileName = null, CancellationToken cancellationToken = default)
    {
        var smtpSection = _configuration.GetSection("Smtp");
        var host = smtpSection["Host"] ?? "localhost";
        var port = int.TryParse(smtpSection["Port"], out var p) ? p : 25;
        var from = smtpSection["From"] ?? "noreply@rhyno.app";
        var displayName = smtpSection["DisplayName"] ?? "راینو ERP";
        var username = smtpSection["Username"];
        var password = smtpSection["Password"];
        var enableSsl = bool.TryParse(smtpSection["EnableSsl"], out var ssl) && ssl;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(from, displayName, Encoding.UTF8),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            message.To.Add(new MailAddress(to));

            if (attachment != null && !string.IsNullOrWhiteSpace(attachmentFileName))
            {
                message.Attachments.Add(new Attachment(
                    new MemoryStream(attachment),
                    attachmentFileName));
            }

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Credentials = string.IsNullOrWhiteSpace(username)
                    ? null
                    : new NetworkCredential(username, password),
                Timeout = 15000
            };

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email sent to {To} with subject '{Subject}'", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email to {To}: {Message}", to, ex.Message);
            // Don't throw - email failures should not block operations
        }
    }

    #region Email Templates

    private static string BuildPasswordResetHtml(string userName, string resetLink)
    {
        var displayName = string.IsNullOrWhiteSpace(userName) ? "کاربر گرامی" : userName;
        return $@"<!DOCTYPE html>
<html lang='fa' dir='rtl'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>بازیابی رمز عبور</title>
<style>
  body {{ font-family: Tahoma, 'Segoe UI', sans-serif; background: #f1f5f9; margin: 0; padding: 24px; }}
  .container {{ max-width: 560px; margin: 0 auto; background: #fff; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.08); overflow: hidden; }}
  .header {{ background: linear-gradient(135deg, #0f766e, #14b8a6); padding: 28px; text-align: center; }}
  .header h1 {{ color: #fff; font-size: 22px; margin: 0; }}
  .body {{ padding: 32px 24px; }}
  .body p {{ color: #334155; font-size: 14px; line-height: 1.8; margin: 0 0 16px; }}
  .btn {{ display: inline-block; background: #0f766e; color: #fff !important; text-decoration: none; padding: 12px 32px; border-radius: 8px; font-size: 15px; font-weight: 600; margin: 12px 0; }}
  .btn:hover {{ background: #0d6b63; }}
  .link {{ direction: ltr; display: block; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 10px 14px; font-size: 12px; color: #64748b; word-break: break-all; margin: 16px 0; text-align: left; }}
  .footer {{ background: #f8fafc; border-top: 1px solid #e2e8f0; padding: 16px 24px; text-align: center; }}
  .footer p {{ color: #94a3b8; font-size: 11px; margin: 0; }}
</style>
</head>
<body>
<div class='container'>
  <div class='header'>
    <h1>راینو ERP</h1>
  </div>
  <div class='body'>
    <p>سلام {WebUtility.HtmlEncode(displayName)}،</p>
    <p>درخواست بازیابی رمز عبور برای حساب کاربری شما ثبت شده است. برای تنظیم رمز عبور جدید، روی دکمه زیر کلیک کنید:</p>
    <div style='text-align: center;'>
      <a href='{WebUtility.HtmlEncode(resetLink)}' class='btn'>بازیابی رمز عبور</a>
    </div>
    <p>اگر دکمه بالا کار نمی‌کند، لینک زیر را در مرورگر خود باز کنید:</p>
    <div class='link'>{WebUtility.HtmlEncode(resetLink)}</div>
    <p>این لینک تا ۲۴ ساعت معتبر است. اگر شما درخواست بازیابی رمز عبور نداده‌اید، این ایمیل را نادیده بگیرید.</p>
  </div>
  <div class='footer'>
    <p>© {DateTime.Now.Year} راینو ERP - تمامی حقوق محفوظ است.</p>
  </div>
</div>
</body>
</html>";
    }

    private static string BuildTaskAssignmentHtml(string userName, string taskTitle, string assignedByName, string taskUrl)
    {
        var displayName = string.IsNullOrWhiteSpace(userName) ? "کاربر گرامی" : userName;
        return $@"<!DOCTYPE html>
<html lang='fa' dir='rtl'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>تخصیص تسک جدید</title>
<style>
  body {{ font-family: Tahoma, 'Segoe UI', sans-serif; background: #f1f5f9; margin: 0; padding: 24px; }}
  .container {{ max-width: 560px; margin: 0 auto; background: #fff; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.08); overflow: hidden; }}
  .header {{ background: linear-gradient(135deg, #4338ca, #6366f1); padding: 28px; text-align: center; }}
  .header h1 {{ color: #fff; font-size: 22px; margin: 0; }}
  .body {{ padding: 32px 24px; }}
  .body p {{ color: #334155; font-size: 14px; line-height: 1.8; margin: 0 0 16px; }}
  .task-box {{ background: #eef2ff; border-left: 4px solid #6366f1; border-radius: 6px; padding: 16px; margin: 16px 0; }}
  .task-box .title {{ font-size: 16px; font-weight: 700; color: #1e293b; margin: 0 0 8px; }}
  .task-box .meta {{ font-size: 13px; color: #64748b; margin: 0; }}
  .btn {{ display: inline-block; background: #4338ca; color: #fff !important; text-decoration: none; padding: 12px 32px; border-radius: 8px; font-size: 15px; font-weight: 600; margin: 8px 0; }}
  .btn:hover {{ background: #3730a3; }}
  .footer {{ background: #f8fafc; border-top: 1px solid #e2e8f0; padding: 16px 24px; text-align: center; }}
  .footer p {{ color: #94a3b8; font-size: 11px; margin: 0; }}
</style>
</head>
<body>
<div class='container'>
  <div class='header'>
    <h1>تسک جدید - راینو ERP</h1>
  </div>
  <div class='body'>
    <p>سلام {WebUtility.HtmlEncode(displayName)}،</p>
    <p>یک تسک جدید به شما اختصاص داده شده است:</p>
    <div class='task-box'>
      <p class='title'>{WebUtility.HtmlEncode(taskTitle)}</p>
      <p class='meta'>اختصاص‌دهنده: {WebUtility.HtmlEncode(assignedByName)}</p>
    </div>
    <div style='text-align: center;'>
      <a href='{WebUtility.HtmlEncode(taskUrl)}' class='btn'>مشاهده تسک</a>
    </div>
  </div>
  <div class='footer'>
    <p>© {DateTime.Now.Year} راینو ERP - تمامی حقوق محفوظ است.</p>
  </div>
</div>
</body>
</html>";
    }

    private static string BuildScheduledReportHtml(string userName, string reportTitle)
    {
        var displayName = string.IsNullOrWhiteSpace(userName) ? "کاربر گرامی" : userName;
        return $@"<!DOCTYPE html>
<html lang='fa' dir='rtl'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>گزارش دوره‌ای</title>
<style>
  body {{ font-family: Tahoma, 'Segoe UI', sans-serif; background: #f1f5f9; margin: 0; padding: 24px; }}
  .container {{ max-width: 560px; margin: 0 auto; background: #fff; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.08); overflow: hidden; }}
  .header {{ background: linear-gradient(135deg, #047857, #10b981); padding: 28px; text-align: center; }}
  .header h1 {{ color: #fff; font-size: 22px; margin: 0; }}
  .body {{ padding: 32px 24px; }}
  .body p {{ color: #334155; font-size: 14px; line-height: 1.8; margin: 0 0 16px; }}
  .report-box {{ background: #ecfdf5; border-left: 4px solid #10b981; border-radius: 6px; padding: 16px; margin: 16px 0; }}
  .report-box .title {{ font-size: 16px; font-weight: 700; color: #1e293b; margin: 0; }}
  .footer {{ background: #f8fafc; border-top: 1px solid #e2e8f0; padding: 16px 24px; text-align: center; }}
  .footer p {{ color: #94a3b8; font-size: 11px; margin: 0; }}
</style>
</head>
<body>
<div class='container'>
  <div class='header'>
    <h1>گزارش خودکار - راینو ERP</h1>
  </div>
  <div class='body'>
    <p>سلام {WebUtility.HtmlEncode(displayName)}،</p>
    <div class='report-box'>
      <p class='title'>{WebUtility.HtmlEncode(reportTitle)}</p>
    </div>
    <p>گزارش زمان‌بندی‌شده مطابق درخواست شما تولید شده و به این ایمیل پیوست شده است.</p>
  </div>
  <div class='footer'>
    <p>© {DateTime.Now.Year} راینو ERP - تمامی حقوق محفوظ است.</p>
  </div>
</div>
</body>
</html>";
    }

    #endregion
}