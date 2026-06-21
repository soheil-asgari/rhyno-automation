using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [PermissionAuthorize("AuditLogs.Read")]
    [Route("admin/audit-dashboard")]
    public class AuditDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            var weekStart = todayStart.AddDays(-6);

            var model = new AuditDashboardViewModel
            {
                TotalEvents = await _context.AuditLogs.CountAsync(cancellationToken),
                SensitiveEventsToday = await _context.AuditLogs.CountAsync(item => item.DateTime >= todayStart && item.IsSensitive, cancellationToken),
                DeletesToday = await _context.AuditLogs.CountAsync(item => item.DateTime >= todayStart && item.Action == "Delete", cancellationToken),
                WeekEvents = await _context.AuditLogs.CountAsync(item => item.DateTime >= weekStart, cancellationToken),
                RecentSecurityEvents = await _context.AuditLogs
                    .AsNoTracking()
                    .Where(item => item.DateTime >= weekStart)
                    .Where(item => item.IsSensitive || item.Action == "Delete")
                    .OrderByDescending(item => item.DateTime)
                    .Take(8)
                    .Select(item => new AuditDashboardItemViewModel
                    {
                        Title = item.Action,
                        When = item.DateTime,
                        Detail = $"{item.TableName} - {(string.IsNullOrWhiteSpace(item.AffectedColumns) ? "بدون ستون مشخص" : item.AffectedColumns)}",
                        Source = string.IsNullOrWhiteSpace(item.UserId) ? "سیستم" : item.UserId
                    })
                    .ToListAsync(cancellationToken)
            };

            return View(model);
        }
    }

    public sealed class AuditDashboardViewModel
    {
        public int TotalEvents { get; init; }
        public int SensitiveEventsToday { get; init; }
        public int DeletesToday { get; init; }
        public int WeekEvents { get; init; }
        public List<AuditDashboardItemViewModel> RecentSecurityEvents { get; init; } = new();
    }

    public sealed class AuditDashboardItemViewModel
    {
        public string Title { get; init; } = string.Empty;
        public DateTimeOffset When { get; init; }
        public string Detail { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
    }
}
