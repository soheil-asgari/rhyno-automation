using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [PermissionAuthorize("Calendar.View")]
    public class OrganizationCalendarController : Controller
    {
        private readonly OfficeDbContext _context;
        private readonly FinanceDbContext _financeContext;

        public OrganizationCalendarController(OfficeDbContext context, FinanceDbContext financeContext)
        {
            _context = context;
            _financeContext = financeContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? year, int? month, CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var pc = new PersianCalendar();
            var targetYear = year ?? pc.GetYear(now);
            var targetMonth = month ?? pc.GetMonth(now);

            var startDate = pc.ToDateTime(targetYear, targetMonth, 1, 0, 0, 0, 0);
            var nextMonthDate = targetMonth == 12
                ? pc.ToDateTime(targetYear + 1, 1, 1, 0, 0, 0, 0)
                : pc.ToDateTime(targetYear, targetMonth + 1, 1, 0, 0, 0, 0);

            var events = new List<CalendarEventItemVM>();
            events.AddRange(await BuildLeaveEventsAsync(startDate, nextMonthDate, cancellationToken));
            events.AddRange(await BuildInvoiceDeadlineEventsAsync(startDate, nextMonthDate, cancellationToken));
            events.AddRange(await BuildPayrollEventsAsync(targetYear, targetMonth, cancellationToken));
            events.AddRange(await BuildManualEventsAsync(startDate, nextMonthDate, cancellationToken));
            events.AddRange(BuildFixedOccasions(targetYear, targetMonth));

            var monthName = GetPersianMonthName(targetMonth);
            var model = new OrganizationCalendarIndexVM
            {
                CurrentYear = targetYear,
                CurrentMonth = targetMonth,
                MonthTitle = $"{monthName} {targetYear}",
                Events = events
                    .OrderBy(item => item.DateGregorian)
                    .ThenBy(item => item.EventType)
                    .ToList(),
                NewEvent = new OrganizationCalendarEventVM
                {
                    EventDateShamsi = $"{targetYear:0000}/{targetMonth:00}/01",
                    SourceModule = "Calendar"
                }
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Calendar.Create")]
        public async Task<IActionResult> CreateEvent(OrganizationCalendarEventVM model, CancellationToken cancellationToken)
        {
            if (!TryParseShamsiDate(model.EventDateShamsi, out var eventDate))
            {
                TempData["CalendarMessage"] = "تاریخ شمسی نامعتبر است.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["CalendarMessage"] = "اطلاعات رخداد تقویم معتبر نیست.";
                return RedirectToAction(nameof(Index));
            }

            _context.OrganizationCalendarEvents.Add(new OrganizationCalendarEvent
            {
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                EventType = model.EventType,
                EventDate = eventDate,
                EventDateShamsi = NormalizeShamsi(model.EventDateShamsi),
                SourceModule = string.IsNullOrWhiteSpace(model.SourceModule) ? "Calendar" : model.SourceModule.Trim(),
                IsAllDay = model.IsAllDay,
                IsSensitive = model.IsSensitive
            });

            await _context.SaveChangesAsync(cancellationToken);
            TempData["CalendarMessage"] = "رخداد تقویم ثبت شد.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<CalendarEventItemVM>> BuildLeaveEventsAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            return await _context.Leaves
                .AsNoTracking()
                .Where(item => item.StartDate <= to && item.EndDate >= from)
                .Select(item => new CalendarEventItemVM
                {
                    Id = item.Id,
                    Title = $"مرخصی: {item.Reason}",
                    EventType = OrganizationCalendarEventTypes.Leave,
                    EventTypeTitle = "مرخصی",
                    DateShamsi = ToShamsi(item.StartDate),
                    DateGregorian = item.StartDate,
                    SourceModule = "HR",
                    IsSystemGenerated = true,
                    IsSensitive = false
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<CalendarEventItemVM>> BuildInvoiceDeadlineEventsAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            var rawItems = await _financeContext.Invoices
                .AsNoTracking()
                .Where(item => item.InvoiceType == "Purchase" && item.DeadlineDateShamsi != null)
                .Select(item => new { item.Id, item.InvoiceNumber, item.PartyName, item.DeadlineDateShamsi, item.GrandTotal })
                .ToListAsync(cancellationToken);

            return rawItems
                .Where(item => TryParseShamsiDate(item.DeadlineDateShamsi, out var parsed) && parsed >= from && parsed < to)
                .Select(item =>
                {
                    TryParseShamsiDate(item.DeadlineDateShamsi, out var parsedDate);
                    return new CalendarEventItemVM
                    {
                        Id = item.Id,
                        Title = $"پرداخت فاکتور {item.InvoiceNumber} - {item.PartyName}",
                        EventType = OrganizationCalendarEventTypes.Payment,
                        EventTypeTitle = "پرداخت",
                        DateShamsi = NormalizeShamsi(item.DeadlineDateShamsi),
                        DateGregorian = parsedDate,
                        SourceModule = "Finance",
                        IsSystemGenerated = true,
                        IsSensitive = item.GrandTotal > 0
                    };
                })
                .ToList();
        }

        private async Task<List<CalendarEventItemVM>> BuildPayrollEventsAsync(int year, int month, CancellationToken cancellationToken)
        {
            var payrollLists = await _financeContext.PayrollLists
                .AsNoTracking()
                .Where(item => item.Year == year && item.Month == month)
                .ToListAsync(cancellationToken);

            return payrollLists.Select(item =>
            {
                var dueDate = new PersianCalendar().ToDateTime(year, month, 27, 0, 0, 0, 0);
                return new CalendarEventItemVM
                {
                    Id = item.Id,
                    Title = $"موعد مالیات/حقوق - لیست #{item.Id}",
                    EventType = OrganizationCalendarEventTypes.Tax,
                    EventTypeTitle = "مالیات/حقوق",
                    DateShamsi = ToShamsi(dueDate),
                    DateGregorian = dueDate,
                    SourceModule = "Finance",
                    IsSystemGenerated = true,
                    IsSensitive = true
                };
            }).ToList();
        }

        private async Task<List<CalendarEventItemVM>> BuildManualEventsAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            return await _context.OrganizationCalendarEvents
                .AsNoTracking()
                .Where(item => item.EventDate >= from && item.EventDate < to)
                .Select(item => new CalendarEventItemVM
                {
                    Id = item.Id,
                    Title = item.Title,
                    EventType = item.EventType,
                    EventTypeTitle = item.EventType,
                    DateShamsi = item.EventDateShamsi ?? string.Empty,
                    DateGregorian = item.EventDate,
                    SourceModule = item.SourceModule,
                    IsSystemGenerated = false,
                    IsSensitive = item.IsSensitive
                })
                .ToListAsync(cancellationToken);
        }

        private static List<CalendarEventItemVM> BuildFixedOccasions(int year, int month)
        {
            var pc = new PersianCalendar();
            var result = new List<CalendarEventItemVM>();
            var occasions = new List<(int Month, int Day, string Title)>
            {
                (1, 1, "آغاز سال نو"),
                (1, 12, "روز جمهوری اسلامی"),
                (11, 22, "سالگرد پیروزی انقلاب"),
                (12, 29, "روز ملی شدن نفت")
            };

            foreach (var occasion in occasions.Where(item => item.Month == month))
            {
                var date = pc.ToDateTime(year, month, occasion.Day, 0, 0, 0, 0);
                result.Add(new CalendarEventItemVM
                {
                    Title = occasion.Title,
                    EventType = OrganizationCalendarEventTypes.Occasion,
                    EventTypeTitle = "مناسبت",
                    DateShamsi = $"{year:0000}/{month:00}/{occasion.Day:00}",
                    DateGregorian = date,
                    SourceModule = "Calendar",
                    IsSystemGenerated = true
                });
            }

            return result;
        }

        private static string GetPersianMonthName(int month)
        {
            return month switch
            {
                1 => "فروردین",
                2 => "اردیبهشت",
                3 => "خرداد",
                4 => "تیر",
                5 => "مرداد",
                6 => "شهریور",
                7 => "مهر",
                8 => "آبان",
                9 => "آذر",
                10 => "دی",
                11 => "بهمن",
                12 => "اسفند",
                _ => "نامشخص"
            };
        }

        private static string ToShamsi(DateTime date)
        {
            var pc = new PersianCalendar();
            return $"{pc.GetYear(date):0000}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00}";
        }

        private static string NormalizeShamsi(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return input.Replace('-', '/').Trim();
        }

        private static bool TryParseShamsiDate(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var cleaned = value.Replace('-', '/').Trim();
            var parts = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var year) ||
                !int.TryParse(parts[1], out var month) ||
                !int.TryParse(parts[2], out var day))
            {
                return false;
            }

            try
            {
                date = new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

