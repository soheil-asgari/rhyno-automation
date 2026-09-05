using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.DTOs.Api.v1;
using OfficeAutomation.Services;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Controllers.Financial
{
    public class VouchersController : Controller
    {
        private readonly FinanceDbContext _context;
        private readonly IUserContextService _userContextService;

        public VouchersController(FinanceDbContext context, IUserContextService userContextService)
        {
            _context = context;
            _userContextService = userContextService;
        }

        [HttpGet("/Finance/Vouchers")]
        public IActionResult Index()
        {
            return PartialView("~/Views/Accounting/_VoucherListPartial.cshtml");
        }

        [HttpGet("/Finance/Vouchers/Create")]
        public async Task<IActionResult> Create()
        {
            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var userContext = !string.IsNullOrWhiteSpace(userId) && User != null
                ? await _userContextService.GetUserContextAsync(userId, User)
                : new UserContextModel();

            // دریافت نام واقعی نمایشی کاربر لاگین‌شده از روی سشن/پروفایل جاری
            ViewBag.CurrentUserName = !string.IsNullOrWhiteSpace(userContext.DisplayName)
                ? userContext.DisplayName
                : (User?.Identity?.Name ?? "کاربر سیستم");

            return PartialView("~/Views/Accounting/_VoucherEditPartial.cshtml");
        }

        [HttpGet("api/vouchers/list")]
        public async Task<IActionResult> GetList([FromQuery] VoucherFilterDto filter)
        {
            try
            {
                var query = _context.Vouchers.AsNoTracking().AsQueryable();

                if (filter.VoucherNo.HasValue)
                    query = query.Where(v => v.VoucherNo == filter.VoucherNo.Value);

                if (!string.IsNullOrWhiteSpace(filter.VoucherDate))
                    query = query.Where(v => v.VoucherDate.Contains(filter.VoucherDate));

                if (!string.IsNullOrWhiteSpace(filter.Status))
                    query = query.Where(v => v.Status == filter.Status);

                if (!string.IsNullOrWhiteSpace(filter.VoucherType))
                    query = query.Where(v => v.VoucherType == filter.VoucherType);

                if (!string.IsNullOrWhiteSpace(filter.Description))
                    query = query.Where(v => v.Description != null && v.Description.Contains(filter.Description));

                if (filter.ReferenceNo.HasValue)
                    query = query.Where(v => v.ReferenceNo == filter.ReferenceNo.Value);

                if (filter.DailyNo.HasValue)
                    query = query.Where(v => v.DailyNo == filter.DailyNo.Value);

                if (!string.IsNullOrWhiteSpace(filter.CreatedBy))
                    query = query.Where(v => v.CreatedBy.Contains(filter.CreatedBy));

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(v => v.VoucherNo)
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToListAsync();

                return Ok(new PagedResult<Voucher>
                {
                    Items = items,
                    TotalCount = totalCount,
                    Page = filter.Page,
                    PageSize = filter.PageSize
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }
    }
}