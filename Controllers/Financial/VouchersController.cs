using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.DTOs.Api.v1;
using OfficeAutomation.Services;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Controllers.Financial
{
    public class VouchersController : Controller
    {
        private readonly FinanceDbContext _context;

        public VouchersController(FinanceDbContext context)
        {
            _context = context;
        }

        [HttpGet("/Finance/Vouchers")]
        public IActionResult Index()
        {
            return PartialView("~/Views/Accounting/_VoucherListPartial.cshtml");
        }

        [HttpGet("/Finance/Vouchers/Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateViewBagsAsync();
            return PartialView("~/Views/Accounting/_VoucherEditPartial.cshtml");
        }

        [HttpGet("/Finance/Vouchers/Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            await PopulateViewBagsAsync();
            var voucher = await _context.VoucherHeaders
                .AsNoTracking()
                .Include(v => v.Lines.OrderBy(l => l.DisplayOrder))
                .Include(v => v.JournalType)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null) return NotFound();

            return PartialView("~/Views/Accounting/_VoucherEditPartial.cshtml", voucher);
        }

        private async Task PopulateViewBagsAsync()
        {
            ViewBag.JournalTypes = await _context.Set<JournalType>()
                .AsNoTracking()
                .Where(j => j.IsActive)
                .OrderBy(j => j.Name)
                .Select(j => new { j.Id, j.Name, j.Code })
                .ToListAsync();

            ViewBag.FiscalYears = await _context.FiscalYears!
                .AsNoTracking()
                .Where(f => !f.IsClosed)
                .OrderByDescending(f => f.StartDate)
                .Select(f => new { f.Id, Name = f.YearName, f.StartDate, f.EndDate })
                .ToListAsync();
        }

        // ====== API Endpoints ======

        [HttpGet("/api/vouchers/list")]
        public async Task<IActionResult> GetList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? voucherNo = null,
            [FromQuery] string? voucherDate = null,
            [FromQuery] string? status = null,
            [FromQuery] string? voucherType = null,
            [FromQuery] string? description = null,
            [FromQuery] string? referenceNo = null,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null)
        {
            try
            {
                var query = _context.VoucherHeaders
                    .AsNoTracking()
                    .Include(v => v.JournalType)
                    .Include(v => v.FiscalYear)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(voucherNo) && int.TryParse(voucherNo, out var vn))
                    query = query.Where(v => v.VoucherNumber == vn);

                if (!string.IsNullOrWhiteSpace(fromDate))
                    query = query.Where(v => v.VoucherDate >= ParsePersianDate(fromDate));

                if (!string.IsNullOrWhiteSpace(toDate))
                    query = query.Where(v => v.VoucherDate <= ParsePersianDate(toDate));

                if (!string.IsNullOrWhiteSpace(status))
                {
                    var statusVal = int.TryParse(status, out var si)
                        ? (VoucherStatus)si
                        : VoucherStatus.Draft;
                    query = query.Where(v => v.Status == statusVal);
                }

                if (!string.IsNullOrWhiteSpace(voucherType))
                    query = query.Where(v => v.JournalType.Name.Contains(voucherType));

                if (!string.IsNullOrWhiteSpace(description))
                    query = query.Where(v => v.Description != null && v.Description.Contains(description));

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(v => v.VoucherDate)
                    .ThenByDescending(v => v.SequenceNumber)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(v => new
                    {
                        id = v.Id,
                        voucherNo = v.VoucherNumber.ToString() ?? v.SequenceNumber.ToString(),
                        voucherDate = v.VoucherDate.ToString("yyyy/MM/dd"),
                        status = v.Status.ToString(),
                        voucherType = v.JournalType.Name,
                        description = v.Description ?? "",
                        referenceNo = v.Reference ?? "",
                        dailyNo = v.SequenceNumber,
                        totalAmount = v.TotalDebits,
                        createdBy = v.IssuedBy ?? "",
                        modifiedBy = v.LastModifiedBy ?? "",
                        checkedBy = ""
                    })
                    .ToListAsync();

                return Ok(new { items, totalCount, page, pageSize });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        [HttpGet("/api/vouchers/subsidiaries")]
        public async Task<IActionResult> GetSubsidiaries([FromQuery] string? q)
        {
            var query = _context.SubsidiaryAccounts
                .AsNoTracking()
                .Include(s => s.GeneralAccount)
                .ThenInclude(g => g.AccountGroup)
                .Where(s => s.IsActive);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(s => s.Code.Contains(q) || s.Name.Contains(q));

            var items = await query
                .OrderBy(s => s.Code)
                .Take(50)
                .Select(s => new
                {
                    id = s.Id,
                    code = s.Code,
                    name = s.Name,
                    generalCode = s.GeneralAccount.Code,
                    generalName = s.GeneralAccount.Name,
                    groupCode = s.GeneralAccount.AccountGroup.Code,
                    groupName = s.GeneralAccount.AccountGroup.Name,
                    nature = s.Nature.ToString()
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("/api/vouchers/floating-details")]
        public async Task<IActionResult> GetFloatingDetails([FromQuery] int? subsidiaryId, [FromQuery] string? q)
        {
            var query = _context.FloatingDetailAccounts
                .AsNoTracking()
                .Where(f => f.IsActive);

            if (subsidiaryId.HasValue)
                query = query.Where(f =>
                    f.SubsidiaryAccountLinks.Any(l => l.SubsidiaryAccountId == subsidiaryId.Value));

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(f => f.Code.Contains(q) || f.Name.Contains(q));

            var items = await query
                .OrderBy(f => f.Code)
                .Take(50)
                .Select(f => new
                {
                    id = f.Id,
                    code = f.Code,
                    name = f.Name,
                    type = f.Type.ToString()
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("/api/vouchers/journal-types")]
        public async Task<IActionResult> GetJournalTypes()
        {
            var items = await _context.Set<JournalType>()
                .AsNoTracking()
                .Where(j => j.IsActive)
                .OrderBy(j => j.Name)
                .Select(j => new { id = j.Id, name = j.Name, code = j.Code ?? "" })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("/api/vouchers/fiscal-years")]
        public async Task<IActionResult> GetFiscalYears()
        {
            var items = await _context.FiscalYears!
                .AsNoTracking()
                .Where(f => !f.IsClosed)
                .OrderByDescending(f => f.StartDate)
                .Select(f => new { id = f.Id, name = f.YearName, startDate = f.StartDate, endDate = f.EndDate })
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost("/api/vouchers/save")]
        public async Task<IActionResult> Save([FromBody] SaveVoucherRequest request)
        {
            try
            {
                if (request.Lines == null || request.Lines.Count < 2)
                    return BadRequest(new { message = "سند باید حداقل دو سطر داشته باشد." });

                decimal totalDebit = 0, totalCredit = 0;
                foreach (var line in request.Lines)
                {
                    if (line.SubsidiaryAccountId <= 0)
                        return BadRequest(new { message = "کد معین برای تمام سطرها الزامی است." });
                    totalDebit += line.DebitAmount;
                    totalCredit += line.CreditAmount;
                }

                if (totalDebit != totalCredit)
                    return BadRequest(new { message = "بدهکار و بستانکار سند تراز نیست." });

                DateTime voucherDate;
                if (!string.IsNullOrWhiteSpace(request.VoucherDate))
                    voucherDate = ParsePersianDate(request.VoucherDate);
                else
                    voucherDate = DateTime.Today;

                var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? "کاربر";

                FiscalYear? fiscalYear = null;
                if (request.FiscalYearId > 0)
                    fiscalYear = await _context.FiscalYears!
                        .FirstOrDefaultAsync(f => f.Id == request.FiscalYearId && !f.IsClosed);

                fiscalYear ??= await _context.FiscalYears!
                    .Where(f => !f.IsClosed && f.StartDate <= voucherDate && f.EndDate >= voucherDate)
                    .FirstOrDefaultAsync();

                var journalType = await _context.Set<JournalType>()
                    .FirstOrDefaultAsync(j => j.Id == request.JournalTypeId);

                var nextSeq = await _context.VoucherHeaders
                    .MaxAsync(v => (int?)v.SequenceNumber) ?? 0;
                nextSeq++;

                var voucher = new VoucherHeader
                {
                    VoucherNumber = nextSeq,
                    DocumentNumber = nextSeq.ToString(),
                    VoucherDate = voucherDate,
                    Status = VoucherStatus.Draft,
                    PostingStatus = PostingStatus.Draft,
                    JournalTypeId = request.JournalTypeId,
                    JournalType = journalType!,
                    FiscalYearId = fiscalYear?.Id ?? 0,
                    FiscalYear = fiscalYear!,
                    Description = request.Description ?? "",
                    Reference = request.ReferenceNo ?? "",
                    SequenceNumber = nextSeq,
                    TotalDebits = totalDebit,
                    TotalCredits = totalCredit,
                    IssuedBy = userName,
                    IssuedAt = DateTimeOffset.UtcNow,
                    LastModifiedBy = userName,
                    LastModifiedAt = DateTimeOffset.UtcNow,
                    Lines = new List<VoucherLine>()
                };

                int displayOrder = 1;
                foreach (var lineReq in request.Lines)
                {
                    var line = new VoucherLine
                    {
                        VoucherHeaderId = voucher.Id,
                        SubsidiaryAccountId = lineReq.SubsidiaryAccountId,
                        FloatingDetailAccountId = lineReq.FloatingDetailAccountId,
                        DebitAmount = lineReq.DebitAmount,
                        CreditAmount = lineReq.CreditAmount,
                        Narration = lineReq.Narration ?? "",
                        DisplayOrder = displayOrder++
                    };
                    voucher.Lines.Add(line);
                }

                _context.VoucherHeaders.Add(voucher);
                await _context.SaveChangesAsync();

                return Ok(new { id = voucher.Id, voucherNo = voucher.VoucherNumber.ToString(), message = "سند با موفقیت ثبت شد." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        [HttpGet("/api/vouchers/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var voucher = await _context.VoucherHeaders
                .AsNoTracking()
                .Include(v => v.JournalType)
                .Include(v => v.FiscalYear)
                .Include(v => v.Lines.OrderBy(l => l.DisplayOrder))
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null) return NotFound(new { message = "سند یافت نشد." });

            var result = new
            {
                id = voucher.Id,
                voucherNo = voucher.VoucherNumber.ToString() ?? "",
                voucherDate = voucher.VoucherDate.ToString("yyyy/MM/dd"),
                status = voucher.Status.ToString(),
                postingStatus = voucher.PostingStatus,
                journalTypeId = voucher.JournalTypeId,
                journalTypeName = voucher.JournalType?.Name ?? "",
                fiscalYearId = voucher.FiscalYearId,
                fiscalYearName = voucher.FiscalYear?.YearName ?? "",
                description = voucher.Description ?? "",
                referenceNo = voucher.Reference ?? "",
                sequenceNumber = voucher.SequenceNumber,
                totalDebits = voucher.TotalDebits,
                totalCredits = voucher.TotalCredits,
                issuedBy = voucher.IssuedBy ?? "",
                lines = voucher.Lines.Select(l => new
                {
                    id = l.Id,
                    subsidiaryAccountId = l.SubsidiaryAccountId,
                    floatingDetailAccountId = l.FloatingDetailAccountId,
                    debitAmount = l.DebitAmount,
                    creditAmount = l.CreditAmount,
                    narration = l.Narration ?? "",
                    displayOrder = l.DisplayOrder
                }).ToList()
            };

            return Ok(result);
        }

        private DateTime ParsePersianDate(string persianDate)
        {
            try
            {
                var parts = persianDate.Split('/');
                if (parts.Length == 3 &&
                    int.TryParse(parts[0], out var y) &&
                    int.TryParse(parts[1], out var m) &&
                    int.TryParse(parts[2], out var d))
                {
                    var calendar = new System.Globalization.PersianCalendar();
                    return calendar.ToDateTime(y, m, d, 0, 0, 0, 0);
                }
            }
            catch { }
            return DateTime.Today;
        }
    }

    public class SaveVoucherRequest
    {
        public List<VoucherLineRequest> Lines { get; set; } = new();
        public string? VoucherDate { get; set; }
        public int JournalTypeId { get; set; }
        public int FiscalYearId { get; set; }
        public string? Description { get; set; }
        public string? ReferenceNo { get; set; }
    }

    public class VoucherLineRequest
    {
        public int SubsidiaryAccountId { get; set; }
        public Guid? FloatingDetailAccountId { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string? Narration { get; set; }
        public int DisplayOrder { get; set; }
    }
}