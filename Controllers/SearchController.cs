using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationFacade _authorizationFacade;

        public SearchController(ApplicationDbContext context, IAuthorizationFacade authorizationFacade)
        {
            _context = context;
            _authorizationFacade = authorizationFacade;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
        {
            var model = new GlobalSearchViewModel { Query = q };
            var term = q?.Trim();

            if (!string.IsNullOrWhiteSpace(term))
            {
                model.Results.AddRange(await SearchLettersAsync(term, cancellationToken));
                model.Results.AddRange(await SearchPeopleAsync(term, cancellationToken));
                model.Results.AddRange(await SearchInvoicesAsync(term, cancellationToken));
                model.Results.AddRange(await SearchProductsAsync(term, cancellationToken));
                model.Results.AddRange(await SearchVendorsAsync(term, cancellationToken));
                model.Results.AddRange(await SearchWaybillsAsync(term, cancellationToken));
                model.Results.AddRange(await SearchUsersAsync(term, cancellationToken));
            }

            model.Results = model.Results.Take(80).ToList();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Quick(string? q, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Json(Array.Empty<object>());
            }

            var term = q.Trim();
            var results = new List<GlobalSearchResultViewModel>();
            results.AddRange(await SearchLettersAsync(term, cancellationToken, 3));
            results.AddRange(await SearchInvoicesAsync(term, cancellationToken, 3));
            results.AddRange(await SearchProductsAsync(term, cancellationToken, 3));
            results.AddRange(await SearchPeopleAsync(term, cancellationToken, 3));

            return Json(results.Take(10).Select(item => new
            {
                item.Module,
                item.Title,
                item.Subtitle,
                item.Url,
                item.Icon,
                item.Tone
            }));
        }

        private async Task<List<GlobalSearchResultViewModel>> SearchLettersAsync(string term, CancellationToken cancellationToken, int take = 12)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = _context.Letters
                .AsNoTracking()
                .Include(item => item.Sender)
                .Include(item => item.Receiver)
                .Where(item => item.Title.Contains(term) || item.Body.Contains(term));

            if (!await _authorizationFacade.IsSecurityAdminAsync(cancellationToken) && !string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(item => item.SenderId == userId || item.ReceiverId == userId || item.FinalReceiverId == userId);
            }

            return await query
                .OrderByDescending(item => item.SentDate)
                .Take(take)
                .Select(item => new GlobalSearchResultViewModel
                {
                    Module = "نامه‌ها",
                    Title = item.Title,
                    Subtitle = (item.Sender!.FullName ?? item.Sender.UserName) + " -> " + (item.Receiver!.FullName ?? item.Receiver.UserName),
                    Url = "/Letters/Details/" + item.Id,
                    Icon = "bi-envelope-paper",
                    Tone = "danger"
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<GlobalSearchResultViewModel>> SearchPeopleAsync(string term, CancellationToken cancellationToken, int take = 12)
        {
            return await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(item =>
                    item.FullName.Contains(term) ||
                    item.PersonnelCode.Contains(term) ||
                    item.NationalCode.Contains(term) ||
                    item.PositionTitle.Contains(term))
                .OrderBy(item => item.FullName)
                .Take(take)
                .Select(item => new GlobalSearchResultViewModel
                {
                    Module = "اشخاص",
                    Title = item.FullName,
                    Subtitle = item.PersonnelCode + " - " + item.PositionTitle,
                    Url = "/HumanCapital/Details/" + item.Id,
                    Icon = "bi-person-badge",
                    Tone = "warning"
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<GlobalSearchResultViewModel>> SearchInvoicesAsync(string term, CancellationToken cancellationToken, int take = 12)
        {
            return await _context.Invoices
                .AsNoTracking()
                .Where(item =>
                    item.InvoiceNumber.Contains(term) ||
                    item.PartyName.Contains(term) ||
                    item.VendorName.Contains(term) ||
                    (item.NationalCodeOrEconomicId ?? string.Empty).Contains(term))
                .OrderByDescending(item => item.CreatedAt)
                .Take(take)
                .Select(item => new GlobalSearchResultViewModel
                {
                    Module = "فاکتورها",
                    Title = item.InvoiceNumber + " - " + item.PartyName,
                    Subtitle = item.InvoiceType + " | " + item.GrandTotal.ToString("N0"),
                    Url = "/Financial/EditInvoice/" + item.Id,
                    Icon = "bi-receipt",
                    Tone = "success"
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<GlobalSearchResultViewModel>> SearchProductsAsync(string term, CancellationToken cancellationToken, int take = 12)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(item => !item.IsDeleted && (item.Name.Contains(term) || item.Code.Contains(term) || (item.Description ?? string.Empty).Contains(term)))
                .OrderBy(item => item.Name)
                .Take(take)
                .Select(item => new GlobalSearchResultViewModel
                {
                    Module = "کالاها",
                    Title = item.Name,
                    Subtitle = item.Code + " | " + item.Unit,
                    Url = "/Warehouse/Products?searchTerm=" + Uri.EscapeDataString(item.Code),
                    Icon = "bi-box-seam",
                    Tone = "primary"
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<GlobalSearchResultViewModel>> SearchVendorsAsync(string term, CancellationToken cancellationToken, int take = 12)
        {
            return await _context.Vendors
                .AsNoTracking()
                .Where(item =>
                    item.Name.Contains(term) ||
                    (item.EconomicCode ?? string.Empty).Contains(term) ||
                    (item.NationalId ?? string.Empty).Contains(term) ||
                    (item.Phone ?? string.Empty).Contains(term))
                .OrderBy(item => item.Name)
                .Take(take)
                .Select(item => new GlobalSearchResultViewModel
                {
                    Module = "تامین‌کنندگان",
                    Title = item.Name,
                    Subtitle = item.EconomicCode ?? item.NationalId ?? item.Phone,
                    Url = "/Vendors?searchTerm=" + Uri.EscapeDataString(item.Name),
                    Icon = "bi-truck",
                    Tone = "secondary"
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<GlobalSearchResultViewModel>> SearchWaybillsAsync(string term, CancellationToken cancellationToken, int take = 12)
        {
            return await _context.Waybills
                .AsNoTracking()
                .Where(item => !item.IsDeleted &&
                    (item.WaybillNumber.Contains(term) ||
                     item.SenderName.Contains(term) ||
                     item.ReceiverName.Contains(term) ||
                     item.DriverName.Contains(term) ||
                     item.OriginCity.Contains(term) ||
                     item.DestinationCity.Contains(term) ||
                     item.VehiclePlateNumber.Contains(term)))
                .OrderByDescending(item => item.IssueDate)
                .Take(take)
                .Select(item => new GlobalSearchResultViewModel
                {
                    Module = "بارنامه‌ها",
                    Title = item.WaybillNumber + " - " + item.DriverName,
                    Subtitle = item.OriginCity + " -> " + item.DestinationCity,
                    Url = "/Waybill/Details/" + item.Id,
                    Icon = "bi-truck-front",
                    Tone = "info"
                })
                .ToListAsync(cancellationToken);
        }

        private async Task<List<GlobalSearchResultViewModel>> SearchUsersAsync(string term, CancellationToken cancellationToken, int take = 12)
        {
            if (!await _authorizationFacade.IsSecurityAdminAsync(cancellationToken))
            {
                return new List<GlobalSearchResultViewModel>();
            }

            return await _context.Users
                .AsNoTracking()
                .Where(item =>
                    (item.FullName ?? string.Empty).Contains(term) ||
                    (item.UserName ?? string.Empty).Contains(term) ||
                    (item.Email ?? string.Empty).Contains(term) ||
                    (item.JobTitle ?? string.Empty).Contains(term))
                .OrderBy(item => item.FullName)
                .Take(take)
                .Select(item => new GlobalSearchResultViewModel
                {
                    Module = "کاربران",
                    Title = item.FullName ?? item.UserName ?? item.Email ?? "کاربر",
                    Subtitle = item.Email,
                    Url = "/Users",
                    Icon = "bi-person-gear",
                    Tone = "dark"
                })
                .ToListAsync(cancellationToken);
        }
    }
}
