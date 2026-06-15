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
        private static readonly CultureInfo FaCulture = CultureInfo.GetCultureInfo("fa-IR");

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var today = DateTime.Today;
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
            var pendingLeaves = await _context.Leaves.CountAsync(item =>
                item.Status == WorkflowStatus.PendingApproval ||
                item.Status == "در انتظار تایید" ||
                item.Status == "در انتظار تأیید",
                cancellationToken);
            var sentLetters = await _context.Letters.CountAsync(cancellationToken);
            var users = await _context.Users.CountAsync(cancellationToken);
            var openInvoices = await _context.Invoices.CountAsync(item =>
                item.WorkflowStatus == WorkflowStatus.Draft ||
                item.WorkflowStatus == WorkflowStatus.Sent ||
                item.WorkflowStatus == WorkflowStatus.PendingApproval,
                cancellationToken);
            var criticalStockItems = await _context.InventoryStocks.CountAsync(item =>
                item.Product.MinimumStock > 0 && item.CurrentQuantity <= item.Product.MinimumStock,
                cancellationToken);
            var pendingTransfers = await _context.InventoryTransferRequests.CountAsync(item =>
                item.Status == WorkflowStatus.PendingApproval || item.Status == "PendingManager",
                cancellationToken);
            var pendingLetters = await _context.Letters.CountAsync(item =>
                item.WorkflowStatus == WorkflowStatus.PendingApproval ||
                (!item.IsWorkflowCompleted && item.CurrentWorkflowStep > 0),
                cancellationToken);
            var todayLetters = await _context.Letters.CountAsync(item => item.SentDate.Date == today, cancellationToken);
            var todayInvoices = await _context.Invoices.CountAsync(item => item.CreatedAt.Date == today, cancellationToken);
            var todayReceipts = await _context.WarehouseReceipts.CountAsync(item => item.CreatedAt.Date == today, cancellationToken);
            var todayIssuances = await _context.WarehouseIssuances.CountAsync(item => item.CreatedAt.Date == today, cancellationToken);
            var utcTodayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            var securityEventsToday = await _context.AuditLogs.CountAsync(item =>
                item.DateTime >= utcTodayStart &&
                (item.Action == "Delete" || item.TableName.Contains("Role") || item.TableName.Contains("Permission")),
                cancellationToken);
            var upcomingPaymentDeadlines = await CountUpcomingInvoiceDeadlinesAsync(cancellationToken);
            var systemWarnings = criticalStockItems + upcomingPaymentDeadlines + securityEventsToday;

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

            var pendingWorkItems = await BuildPendingWorkItemsAsync(cancellationToken);
            var systemAlerts = BuildSystemAlerts(criticalStockItems, upcomingPaymentDeadlines, securityEventsToday);

            var totalWorkItems = newLetters + pendingLeaves + sentLetters;
            var workloadPercent = totalWorkItems == 0
                ? 0
                : Math.Clamp((int)Math.Round((newLetters + pendingLeaves) * 100d / totalWorkItems), 0, 100);

            var todayWorkItems = todayLetters + todayInvoices + todayReceipts + todayIssuances;
            var totalPendingApprovals = pendingLeaves + pendingLetters + pendingTransfers;

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
                TodayWorkItems = todayWorkItems,
                OpenInvoices = openInvoices,
                CriticalStockItems = criticalStockItems,
                ImportantLetters = pendingLetters + newLetters,
                SystemWarnings = systemWarnings,
                ExecutiveMetrics =
                [
                    new DashboardMetricCard
                    {
                        Title = "کارهای امروز",
                        Value = ToFa(todayWorkItems),
                        Description = "نامه، فاکتور و اسناد انبار ثبت شده امروز",
                        Icon = "bi-lightning-charge",
                        Tone = "primary",
                        Url = "#"
                    },
                    new DashboardMetricCard
                    {
                        Title = "در انتظار تایید",
                        Value = ToFa(totalPendingApprovals),
                        Description = "مرخصی، نامه و درخواست انتقال",
                        Icon = "bi-hourglass-split",
                        Tone = "warning",
                        Url = "#pending-work"
                    },
                    new DashboardMetricCard
                    {
                        Title = "فاکتورهای باز",
                        Value = ToFa(openInvoices),
                        Description = "پیش نویس، ارسال شده یا در انتظار تایید",
                        Icon = "bi-receipt-cutoff",
                        Tone = "success",
                        Url = "/Financial/Invoices"
                    },
                    new DashboardMetricCard
                    {
                        Title = "موجودی بحرانی",
                        Value = ToFa(criticalStockItems),
                        Description = "کالاهای رسیده به حداقل موجودی",
                        Icon = "bi-box-seam",
                        Tone = "danger",
                        Url = "/Warehouse/Stock"
                    },
                    new DashboardMetricCard
                    {
                        Title = "هشدارهای سیستم",
                        Value = ToFa(systemWarnings),
                        Description = "امنیت، سررسیدها و هشدارهای عملیاتی",
                        Icon = "bi-bell",
                        Tone = "info",
                        Url = "#system-alerts"
                    }
                ],
                PendingWorkItems = pendingWorkItems,
                SystemAlerts = systemAlerts,
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

        private async Task<List<DashboardWorkItem>> BuildPendingWorkItemsAsync(CancellationToken cancellationToken)
        {
            var items = new List<DashboardWorkItem>();

            items.AddRange(await _context.Leaves
                .AsNoTracking()
                .Include(item => item.User)
                .Where(item =>
                    item.Status == WorkflowStatus.PendingApproval ||
                    item.Status == "در انتظار تایید" ||
                    item.Status == "در انتظار تأیید")
                .OrderByDescending(item => item.StartDate)
                .Take(4)
                .Select(item => new DashboardWorkItem
                {
                    Title = item.User != null ? $"مرخصی {item.User.FullName}" : "درخواست مرخصی",
                    Module = "منابع انسانی",
                    Status = item.Status,
                    StatusCssClass = WorkflowStatus.BadgeCss(item.Status),
                    DateText = ToPersianDate(new DateTimeOffset(item.StartDate)),
                    Url = "/Leaves",
                    Icon = "bi-calendar-check"
                })
                .ToListAsync(cancellationToken));

            items.AddRange(await _context.Letters
                .AsNoTracking()
                .Where(item =>
                    item.WorkflowStatus == WorkflowStatus.PendingApproval ||
                    (!item.IsWorkflowCompleted && item.CurrentWorkflowStep > 0))
                .OrderByDescending(item => item.SentDate)
                .Take(4)
                .Select(item => new DashboardWorkItem
                {
                    Title = item.Title,
                    Module = "نامه ها",
                    Status = item.WorkflowStatus,
                    StatusCssClass = WorkflowStatus.BadgeCss(item.WorkflowStatus),
                    DateText = ToPersianDate(new DateTimeOffset(item.SentDate)),
                    Url = $"/Letters/Details/{item.Id}",
                    Icon = "bi-envelope-paper"
                })
                .ToListAsync(cancellationToken));

            items.AddRange(await _context.Invoices
                .AsNoTracking()
                .Where(item =>
                    item.WorkflowStatus == WorkflowStatus.Draft ||
                    item.WorkflowStatus == WorkflowStatus.Sent ||
                    item.WorkflowStatus == WorkflowStatus.PendingApproval)
                .OrderByDescending(item => item.CreatedAt)
                .Take(4)
                .Select(item => new DashboardWorkItem
                {
                    Title = item.InvoiceNumber + " - " + item.PartyName,
                    Module = item.InvoiceType == "Purchase" ? "فاکتور خرید" : "فاکتور فروش",
                    Status = item.WorkflowStatus,
                    StatusCssClass = WorkflowStatus.BadgeCss(item.WorkflowStatus),
                    DateText = item.DateShamsi,
                    Url = $"/Financial/EditInvoice/{item.Id}",
                    Icon = "bi-receipt"
                })
                .ToListAsync(cancellationToken));

            return items
                .OrderBy(item => WorkflowStatus.IsPending(item.Status) ? 0 : 1)
                .ThenByDescending(item => item.DateText)
                .Take(10)
                .ToList();
        }

        private static List<DashboardSystemAlert> BuildSystemAlerts(int criticalStockItems, int upcomingPaymentDeadlines, int securityEventsToday)
        {
            var alerts = new List<DashboardSystemAlert>();

            if (criticalStockItems > 0)
            {
                alerts.Add(new DashboardSystemAlert
                {
                    Title = "موجودی بحرانی",
                    Description = $"{ToFa(criticalStockItems)} قلم کالا به حداقل موجودی رسیده است.",
                    Severity = "danger",
                    Url = "/Warehouse/Stock",
                    Icon = "bi-exclamation-triangle"
                });
            }

            if (upcomingPaymentDeadlines > 0)
            {
                alerts.Add(new DashboardSystemAlert
                {
                    Title = "سررسید پرداخت/مالیات",
                    Description = $"{ToFa(upcomingPaymentDeadlines)} فاکتور خرید نزدیک موعد پیگیری است.",
                    Severity = "warning",
                    Url = "/Financial/Purchases",
                    Icon = "bi-calendar2-week"
                });
            }

            if (securityEventsToday > 0)
            {
                alerts.Add(new DashboardSystemAlert
                {
                    Title = "تغییرات امنیتی امروز",
                    Description = $"{ToFa(securityEventsToday)} رخداد حساس امنیتی ثبت شده است.",
                    Severity = "info",
                    Url = "/admin/audit-dashboard",
                    Icon = "bi-shield-check"
                });
            }

            if (alerts.Count == 0)
            {
                alerts.Add(new DashboardSystemAlert
                {
                    Title = "بدون هشدار بحرانی",
                    Description = "موجودی، سررسیدها و رخدادهای حساس در وضعیت عادی هستند.",
                    Severity = "success",
                    Url = "#",
                    Icon = "bi-check2-circle"
                });
            }

            return alerts;
        }

        private async Task<int> CountUpcomingInvoiceDeadlinesAsync(CancellationToken cancellationToken)
        {
            var candidates = await _context.Invoices
                .AsNoTracking()
                .Where(item =>
                    item.InvoiceType == "Purchase" &&
                    item.DeadlineDateShamsi != null &&
                    item.WorkflowStatus != WorkflowStatus.Approved &&
                    item.WorkflowStatus != WorkflowStatus.Archived)
                .Select(item => item.DeadlineDateShamsi!)
                .ToListAsync(cancellationToken);

            var today = DateTime.Today;
            var maxDate = today.AddDays(7);
            return candidates.Count(item =>
                TryParseShamsiDate(item, out var deadline) &&
                deadline.Date >= today &&
                deadline.Date <= maxDate);
        }

        private static string ToPersianDate(DateTimeOffset value)
        {
            var local = value.ToLocalTime().DateTime;
            var calendar = new PersianCalendar();
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{calendar.GetYear(local):0000}/{calendar.GetMonth(local):00}/{calendar.GetDayOfMonth(local):00}");
        }

        private static string ToFa(int value) => value.ToString("N0", FaCulture);

        private static bool TryParseShamsiDate(string? value, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Replace('-', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out var year) ||
                !int.TryParse(parts[1], out var month) ||
                !int.TryParse(parts[2], out var day))
            {
                return false;
            }

            try
            {
                result = new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0);
                return true;
            }
            catch
            {
                return false;
            }
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
