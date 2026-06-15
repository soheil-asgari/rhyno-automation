using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new ReportsIndexViewModel
            {
                Modules =
                [
                    new ReportModuleViewModel
                    {
                        Title = "مالی",
                        Description = "فاکتورهای فروش/خرید، مالیات، معاملات فصلی و چاپ فاکتور",
                        Icon = "bi-cash-coin",
                        Tone = "success",
                        Actions =
                        [
                            new() { Label = "اکسل فاکتورهای فروش", Url = Url.Action("ExportInvoicesExcel", "Financial", new { invoiceType = "Sale" }) ?? "#", Kind = "Excel" },
                            new() { Label = "اکسل فاکتورهای خرید", Url = Url.Action("ExportInvoicesExcel", "Financial", new { invoiceType = "Purchase" }) ?? "#", Kind = "Excel" },
                            new() { Label = "داشبورد VAT", Url = Url.Action("VatDashboard", "Financial") ?? "#", Kind = "Report" },
                            new() { Label = "معاملات فصلی", Url = Url.Action("SeasonalTax", "Financial") ?? "#", Kind = "Report" }
                        ]
                    },
                    new ReportModuleViewModel
                    {
                        Title = "انبار",
                        Description = "کالاها، تامین‌کنندگان، موجودی، رسید و حواله",
                        Icon = "bi-box-seam",
                        Tone = "primary",
                        Actions =
                        [
                            new() { Label = "اکسل موجودی", Url = Url.Action("ExportStockExcel", "Financial") ?? "#", Kind = "Excel" },
                            new() { Label = "اکسل کالاها", Url = Url.Action(nameof(ExportProductsExcel)) ?? "#", Kind = "Excel" },
                            new() { Label = "اکسل تامین‌کنندگان", Url = Url.Action(nameof(ExportVendorsExcel)) ?? "#", Kind = "Excel" },
                            new() { Label = "اکسل بارنامه‌ها", Url = Url.Action(nameof(ExportWaybillsExcel)) ?? "#", Kind = "Excel" }
                        ]
                    },
                    new ReportModuleViewModel
                    {
                        Title = "اداری و نامه‌ها",
                        Description = "نامه‌های ارسال‌شده، دریافت‌شده، وضعیت خوانده‌شدن و گردش کار",
                        Icon = "bi-envelope-paper",
                        Tone = "danger",
                        Actions =
                        [
                            new() { Label = "اکسل نامه‌ها", Url = Url.Action(nameof(ExportLettersExcel)) ?? "#", Kind = "Excel" },
                            new() { Label = "جستجوی نامه‌ها", Url = Url.Action("Index", "Search") ?? "#", Kind = "Search" }
                        ]
                    },
                    new ReportModuleViewModel
                    {
                        Title = "حقوق، بیمه و کاربران",
                        Description = "حقوق، لیست بیمه، اشخاص و کاربران سیستم",
                        Icon = "bi-people",
                        Tone = "warning",
                        Actions =
                        [
                            new() { Label = "اکسل حقوق", Url = Url.Action("ExportPayrollExcel", "Financial") ?? "#", Kind = "Excel" },
                            new() { Label = "اکسل بیمه", Url = Url.Action(nameof(ExportInsuranceExcel)) ?? "#", Kind = "Excel" },
                            new() { Label = "اکسل اشخاص", Url = Url.Action(nameof(ExportPeopleExcel)) ?? "#", Kind = "Excel" },
                            new() { Label = "اکسل کاربران", Url = Url.Action(nameof(ExportUsersExcel)) ?? "#", Kind = "Excel" }
                        ]
                    }
                ]
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportLettersExcel(string? q, CancellationToken cancellationToken)
        {
            var query = _context.Letters.AsNoTracking().Include(item => item.Sender).Include(item => item.Receiver).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(item => item.Title.Contains(term) || item.Body.Contains(term));
            }

            var rows = await query.OrderByDescending(item => item.SentDate).Take(5000).ToListAsync(cancellationToken);
            return ExcelFile("letters.xlsx", "Letters", ["شناسه", "موضوع", "فرستنده", "گیرنده", "تاریخ", "وضعیت", "خوانده شده"], rows.Select(item => new object?[]
            {
                item.Id,
                item.Title,
                item.Sender?.FullName ?? item.Sender?.UserName,
                item.Receiver?.FullName ?? item.Receiver?.UserName,
                item.SentDate.ToString("yyyy/MM/dd HH:mm"),
                WorkflowStatus.Label(item.WorkflowStatus),
                item.IsRead ? "بله" : "خیر"
            }));
        }

        [HttpGet]
        public async Task<IActionResult> ExportWaybillsExcel(string? q, CancellationToken cancellationToken)
        {
            var query = _context.Waybills.AsNoTracking().Where(item => !item.IsDeleted);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(item =>
                    item.WaybillNumber.Contains(term) ||
                    item.DriverName.Contains(term) ||
                    item.OriginCity.Contains(term) ||
                    item.DestinationCity.Contains(term));
            }

            var rows = await query.OrderByDescending(item => item.IssueDate).Take(5000).ToListAsync(cancellationToken);
            return ExcelFile("waybills.xlsx", "Waybills", ["شماره", "تاریخ", "فرستنده", "گیرنده", "مسیر", "راننده", "پلاک", "بار", "کرایه", "وضعیت پرداخت"], rows.Select(item => new object?[]
            {
                item.WaybillNumber,
                item.IssueDate.ToString("yyyy/MM/dd"),
                item.SenderName,
                item.ReceiverName,
                item.OriginCity + " -> " + item.DestinationCity,
                item.DriverName,
                item.VehiclePlateNumber,
                item.CargoType,
                item.TotalFreightCharges,
                item.PaymentStatus
            }));
        }

        [HttpGet]
        public async Task<IActionResult> ExportProductsExcel(string? q, CancellationToken cancellationToken)
        {
            var query = _context.Products.AsNoTracking().Where(item => !item.IsDeleted);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(item => item.Code.Contains(term) || item.Name.Contains(term));
            }

            var rows = await query.OrderBy(item => item.Name).Take(5000).ToListAsync(cancellationToken);
            return ExcelFile("products.xlsx", "Products", ["کد", "نام", "واحد", "حداقل موجودی", "فعال"], rows.Select(item => new object?[]
            {
                item.Code,
                item.Name,
                item.Unit,
                item.MinimumStock,
                item.IsActive ? "بله" : "خیر"
            }));
        }

        [HttpGet]
        public async Task<IActionResult> ExportVendorsExcel(string? q, CancellationToken cancellationToken)
        {
            var query = _context.Vendors.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(item => item.Name.Contains(term) || (item.EconomicCode ?? string.Empty).Contains(term) || (item.NationalId ?? string.Empty).Contains(term));
            }

            var rows = await query.OrderBy(item => item.Name).Take(5000).ToListAsync(cancellationToken);
            return ExcelFile("vendors.xlsx", "Vendors", ["نام", "کد اقتصادی", "شناسه ملی", "تلفن", "فعال"], rows.Select(item => new object?[]
            {
                item.Name,
                item.EconomicCode,
                item.NationalId,
                item.Phone,
                item.IsActive ? "بله" : "خیر"
            }));
        }

        [HttpGet]
        public async Task<IActionResult> ExportPeopleExcel(string? q, CancellationToken cancellationToken)
        {
            var query = _context.HumanCapitalEmployees.AsNoTracking().Include(item => item.Department).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(item => item.FullName.Contains(term) || item.PersonnelCode.Contains(term) || item.NationalCode.Contains(term));
            }

            var rows = await query.OrderBy(item => item.FullName).Take(5000).ToListAsync(cancellationToken);
            return ExcelFile("people.xlsx", "People", ["کد پرسنلی", "نام", "کد ملی", "واحد", "سمت", "وضعیت"], rows.Select(item => new object?[]
            {
                item.PersonnelCode,
                item.FullName,
                item.NationalCode,
                item.Department?.Name,
                item.PositionTitle,
                item.CurrentStatus
            }));
        }

        [HttpGet]
        public async Task<IActionResult> ExportUsersExcel(string? q, CancellationToken cancellationToken)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var query = _context.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(item => (item.FullName ?? string.Empty).Contains(term) || (item.Email ?? string.Empty).Contains(term));
            }

            var rows = await query.OrderBy(item => item.FullName).Take(5000).ToListAsync(cancellationToken);
            return ExcelFile("users.xlsx", "Users", ["نام", "ایمیل", "سمت", "مدیر", "مالی", "انبار", "منابع انسانی"], rows.Select(item => new object?[]
            {
                item.FullName,
                item.Email,
                item.JobTitle,
                item.IsManager ? "بله" : "خیر",
                item.CanAccessFinance ? "بله" : "خیر",
                item.CanAccessWarehouse ? "بله" : "خیر",
                item.CanAccessHumanCapital ? "بله" : "خیر"
            }));
        }

        [HttpGet]
        public async Task<IActionResult> ExportInsuranceExcel(string? q, CancellationToken cancellationToken)
        {
            var query = _context.InsuranceLists.AsNoTracking().Include(item => item.Employees).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(item => item.ProjectName.Contains(term) || item.ManagerName.Contains(term) || item.Status.Contains(term));
            }

            var lists = await query.OrderByDescending(item => item.Year).ThenByDescending(item => item.Month).Take(1000).ToListAsync(cancellationToken);
            var rows = lists.SelectMany(list => list.Employees.Select(employee => new object?[]
            {
                list.Year,
                list.Month,
                list.ProjectName,
                list.ManagerName,
                employee.FullName,
                employee.JobTitle,
                employee.WorkDays,
                employee.Salary,
                list.Status
            }));

            return ExcelFile("insurance.xlsx", "Insurance", ["سال", "ماه", "پروژه", "مدیر", "کارمند", "سمت", "روز کارکرد", "حقوق", "وضعیت"], rows);
        }

        private static FileContentResult ExcelFile(string fileName, string sheetName, IReadOnlyList<string> headers, IEnumerable<object?[]> rows)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);
            worksheet.RightToLeft = true;

            for (var column = 0; column < headers.Count; column++)
            {
                worksheet.Cell(1, column + 1).Value = headers[column];
                worksheet.Cell(1, column + 1).Style.Font.Bold = true;
            }

            var rowIndex = 2;
            foreach (var row in rows)
            {
                for (var column = 0; column < row.Length; column++)
                {
                    worksheet.Cell(rowIndex, column + 1).Value = XLCellValue.FromObject(row[column]);
                }

                rowIndex++;
            }

            worksheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return new FileContentResult(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            {
                FileDownloadName = fileName
            };
        }
    }
}
