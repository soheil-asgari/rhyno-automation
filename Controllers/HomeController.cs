using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var weekStart = new DateTimeOffset(now.UtcDateTime.Date.AddDays(-6), TimeSpan.Zero);

            var auditLogs = await _context.AuditLogs
                .AsNoTracking()
                .Where(item => item.DateTime >= weekStart)
                .Select(item => new
                {
                    item.Action,
                    item.TableName,
                    item.DateTime
                })
                .ToListAsync(cancellationToken);

            var totalAuditEvents = await _context.AuditLogs.CountAsync(cancellationToken);
            var createEvents = await _context.AuditLogs.CountAsync(item => item.Action == "Create" || item.Action == "Insert", cancellationToken);
            var updateEvents = await _context.AuditLogs.CountAsync(item => item.Action == "Update" || item.Action == "Edit", cancellationToken);
            var deleteEvents = await _context.AuditLogs.CountAsync(item => item.Action == "Delete", cancellationToken);
            var newLetters = await _context.Letters.CountAsync(item => !item.IsRead, cancellationToken);
            var pendingLeaves = await _context.Leaves.CountAsync(item => item.Status.Contains("انتظار"), cancellationToken);
            var sentLetters = await _context.Letters.CountAsync(cancellationToken);
            var users = await _context.Users.CountAsync(cancellationToken);

            var maxDailyCount = Math.Max(1, auditLogs
                .GroupBy(item => item.DateTime.Date)
                .Select(group => group.Count())
                .DefaultIfEmpty(0)
                .Max());

            var weeklyActivity = Enumerable.Range(0, 7)
                .Select(offset => weekStart.AddDays(offset))
                .Select(day =>
                {
                    var count = auditLogs.Count(item => item.DateTime.Date == day);
                    return new DashboardChartPoint
                    {
                        Label = ToPersianDate(day),
                        Value = count,
                        Percent = Math.Max(8, (int)Math.Round(count * 100d / maxDailyCount))
                    };
                })
                .ToList();

            var recentActivities = auditLogs
                .OrderByDescending(item => item.DateTime)
                .Take(5)
                .Select(item => new DashboardRecentActivity
                {
                    DocumentType = item.TableName,
                    DateText = ToPersianDate(item.DateTime),
                    StatusText = NormalizeActionLabel(item.Action),
                    StatusCssClass = NormalizeActionCss(item.Action)
                })
                .ToList();

            var totalWorkItems = newLetters + pendingLeaves + sentLetters;
            var workloadPercent = totalWorkItems == 0
                ? 0
                : Math.Clamp((int)Math.Round((newLetters + pendingLeaves) * 100d / totalWorkItems), 0, 100);

            var model = new DashboardIndexViewModel
            {
                TodayText = ToPersianDate(now),
                TotalAuditEvents = totalAuditEvents,
                CreateEvents = createEvents,
                UpdateEvents = updateEvents,
                DeleteEvents = deleteEvents,
                NewLetters = newLetters,
                PendingLeaves = pendingLeaves,
                SentLetters = sentLetters,
                TotalUsers = users,
                WorkloadPercent = workloadPercent,
                WeeklyActivity = weeklyActivity,
                RecentActivities = recentActivities
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static string ToPersianDate(DateTimeOffset value)
        {
            var local = value.ToLocalTime().DateTime;
            var calendar = new PersianCalendar();
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{calendar.GetYear(local):0000}/{calendar.GetMonth(local):00}/{calendar.GetDayOfMonth(local):00}");
        }

        private static string NormalizeActionLabel(string action)
        {
            return action?.ToLowerInvariant() switch
            {
                "create" or "insert" => "ثبت شده",
                "update" or "edit" => "ویرایش شده",
                "delete" => "حذف شده",
                _ => "ثبت سیستم"
            };
        }

        private static string NormalizeActionCss(string action)
        {
            return action?.ToLowerInvariant() switch
            {
                "create" or "insert" => "success",
                "update" or "edit" => "pending",
                "delete" => "danger",
                _ => "neutral"
            };
        }
    }
}
