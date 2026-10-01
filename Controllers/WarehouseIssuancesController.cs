using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [RequireAccessArea("Warehouse")]
    [RequiresModule(LicensableModules.Warehouse)]
public class WarehouseIssuancesController : Controller
    {
        private readonly InventoryDbContext _context;
        private readonly FinanceDbContext _financeContext;
        private readonly IdentityDbContext _identityContext;
        private readonly IAuthorizationFacade _authorizationFacade;
        private readonly WorkflowService _workflowService;
        private const int DefaultWarehouseId = 1;

        public WarehouseIssuancesController(
            InventoryDbContext context,
            FinanceDbContext financeContext,
            IdentityDbContext identityContext,
            IAuthorizationFacade authorizationFacade,
            WorkflowService workflowService)
        {
            _context = context;
            _financeContext = financeContext;
            _identityContext = identityContext;
            _authorizationFacade = authorizationFacade;
            _workflowService = workflowService;
        }

        private string? CurrentUserId => User?.Identity?.IsAuthenticated == true ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;

        [HttpGet]
        public async Task<IActionResult> Issuances(string? searchTerm, string? status, int? warehouseId, int? employerId, string? dateFrom, string? dateTo, CancellationToken cancellationToken)
        {
            var query = _context.WarehouseIssuances
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Employer)
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item => item.IssuanceNumber.Contains(term) || item.DestinationOrDepartment.Contains(term));
            }

            if (warehouseId.HasValue)
            {
                query = query.Where(item => item.WarehouseId == warehouseId.Value);
            }

            if (employerId.HasValue)
            {
                query = query.Where(item => item.EmployerId == employerId.Value);
            }

            var items = await query
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(status))
            {
                items = items.Where(item => WorkflowStatus.Normalize(item.WorkflowStatus) == WorkflowStatus.Normalize(status)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var fromDate))
            {
                items = items.Where(item => DateTime.TryParse(item.DateShamsi.Replace('/', '-'), out var docDate) ? docDate >= fromDate.Date : true).ToList();
            }

            if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var toDate))
            {
                items = items.Where(item => DateTime.TryParse(item.DateShamsi.Replace('/', '-'), out var docDate) ? docDate <= toDate.Date : true).ToList();
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.Status = status;
            ViewBag.WarehouseId = warehouseId;
            ViewBag.EmployerId = employerId;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(warehouseId, cancellationToken);
            ViewBag.EmployerOptions = await BuildEmployerOptionsAsync(employerId, cancellationToken);
            return View("~/Views/Warehouse/Issuances.cshtml", items);
        }

        [HttpGet]
        public async Task<IActionResult> IssuanceDetails(int id, CancellationToken cancellationToken)
        {
            var issuance = await _context.WarehouseIssuances
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Employer)
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (issuance == null)
            {
                return NotFound();
            }

            var model = new WarehouseIssuanceDetailsVM
            {
                Issuance = issuance,
                TotalQuantity = issuance.Items.Sum(item => item.Quantity),
                AuditEntries = await BuildAuditEntriesAsync("WarehouseIssuance", id.ToString(), cancellationToken),
                MovementEntries = await BuildMovementEntriesAsync("WarehouseIssuance", id.ToString(), cancellationToken)
            };

            return View("~/Views/Warehouse/IssuanceDetails.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Warehouse.Approve")]
        public async Task<IActionResult> ApproveIssuance(int id, CancellationToken cancellationToken)
        {
            var issuance = await _context.WarehouseIssuances
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (issuance == null)
            {
                return NotFound();
            }

            if (WorkflowStatus.IsApproved(issuance.WorkflowStatus))
            {
                return RedirectToAction(nameof(IssuanceDetails), new { id });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var itemVMs = issuance.Items.Select(item => new WarehouseIssuanceItemVM
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity
            }).ToList();

            var applyResult = await ApplyIssuanceToStockWithRetryAsync(issuance.WarehouseId, itemVMs, "WarehouseIssuance", issuance.Id.ToString(), issuance.IssuanceNumber, cancellationToken);
            if (!applyResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["WarehouseMessage"] = applyResult.Message ?? "ثبت خروج با خطا مواجه شد.";
                return RedirectToAction(nameof(IssuanceDetails), new { id });
            }

            issuance.WorkflowStatus = WorkflowStatus.Approved;
            await _context.SaveChangesAsync(cancellationToken);
            await RecordWarehouseDecisionAsync(nameof(WarehouseIssuance), issuance.Id, WorkflowStatus.Approved, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync("Edit", "WarehouseIssuance", issuance.Id.ToString(), new { Status = issuance.WorkflowStatus }, new { Status = WorkflowStatus.Approved }, cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
            return RedirectToAction(nameof(IssuanceDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectIssuance(int id, CancellationToken cancellationToken)
        {
            var issuance = await _context.WarehouseIssuances.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (issuance == null)
            {
                return NotFound();
            }

            issuance.WorkflowStatus = WorkflowStatus.Rejected;
            await _context.SaveChangesAsync(cancellationToken);
            await RecordWarehouseDecisionAsync(nameof(WarehouseIssuance), issuance.Id, WorkflowStatus.Rejected, null, cancellationToken);
            await WriteAuditLogAsync("Edit", "WarehouseIssuance", issuance.Id.ToString(), new { Status = issuance.WorkflowStatus }, new { Status = WorkflowStatus.Rejected }, cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
            return RedirectToAction(nameof(IssuanceDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelIssuance(int id, CancellationToken cancellationToken)
        {
            var issuance = await _context.WarehouseIssuances.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (issuance == null)
            {
                return NotFound();
            }

            issuance.WorkflowStatus = WorkflowStatus.Canceled;
            await _context.SaveChangesAsync(cancellationToken);
            await WriteAuditLogAsync("Edit", "WarehouseIssuance", issuance.Id.ToString(), new { Status = issuance.WorkflowStatus }, new { Status = WorkflowStatus.Canceled }, cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
            return RedirectToAction(nameof(IssuanceDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReopenIssuance(int id, CancellationToken cancellationToken)
        {
            var issuance = await _context.WarehouseIssuances.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (issuance == null)
            {
                return NotFound();
            }

            issuance.WorkflowStatus = WorkflowStatus.Draft;
            await _context.SaveChangesAsync(cancellationToken);
            await WriteAuditLogAsync("Edit", "WarehouseIssuance", issuance.Id.ToString(), new { Status = issuance.WorkflowStatus }, new { Status = WorkflowStatus.Draft }, cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
            return RedirectToAction(nameof(IssuanceDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> CreateIssuance(CancellationToken cancellationToken)
        {
            var model = new WarehouseIssuanceUpsertVM
            {
                DateShamsi = GetTodayShamsi(),
                IssuanceNumber = await BuildNextIssuanceNumberAsync(cancellationToken),
                WarehouseId = DefaultWarehouseId,
                Items = new List<WarehouseIssuanceItemVM> { new() },
                EmployerId = null
            };

            await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
            await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
            await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
            ViewBag.StockSnapshotJson = JsonSerializer.Serialize(await BuildWarehouseStockSnapshotAsync(model.WarehouseId, cancellationToken), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return View("~/Views/Warehouse/CreateIssuance.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateIssuance(WarehouseIssuanceUpsertVM model, CancellationToken cancellationToken)
        {
            var validItems = model.Items.Where(item => item.ProductId > 0 && item.Quantity > 0).ToList();
            await ValidateIssuanceAsync(model, validItems, cancellationToken);

            if (!ModelState.IsValid)
            {
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
                await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
                ViewBag.StockSnapshotJson = JsonSerializer.Serialize(await BuildWarehouseStockSnapshotAsync(model.WarehouseId, cancellationToken), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return View("~/Views/Warehouse/CreateIssuance.cshtml", model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var entity = new WarehouseIssuance
            {
                IssuanceNumber = model.IssuanceNumber.Trim(),
                DateShamsi = model.DateShamsi.Trim(),
                EmployerId = model.EmployerId,
                DestinationOrDepartment = (await ResolveEmployerNameAsync(model.EmployerId, model.DestinationOrDepartment, cancellationToken)).Trim(),
                Notes = model.Notes?.Trim(),
                WorkflowStatus = model.SaveAsDraft ? WorkflowStatus.Draft : WorkflowStatus.PendingApproval,
                WarehouseId = model.WarehouseId,
                CreatedAt = DateTime.Now,
                Items = validItems.Select(item => new WarehouseIssuanceItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                }).ToList()
            };

            _context.WarehouseIssuances.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            if (entity.WorkflowStatus == WorkflowStatus.PendingApproval)
            {
                await StartWarehouseDocumentWorkflowAsync(
                    nameof(WarehouseIssuance),
                    entity.Id,
                    entity.WarehouseId,
                    CurrentUserId ?? string.Empty,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Create",
                "WarehouseIssuance",
                entity.Id.ToString(),
                null,
                new
                {
                    entity.IssuanceNumber,
                    entity.DateShamsi,
                    entity.DestinationOrDepartment,
                    entity.WarehouseId,
                    ItemCount = entity.Items.Count,
                    entity.WorkflowStatus
                },
                cancellationToken);

            return RedirectToAction(nameof(IssuanceDetails), new { id = entity.Id });
        }

        private async Task<List<WarehouseStockSnapshotVM>> BuildWarehouseStockSnapshotAsync(int warehouseId, CancellationToken cancellationToken)
        {
            return await _context.InventoryStocks
                .AsNoTracking()
                .Include(item => item.Product)
                .Where(item => item.WarehouseId == warehouseId)
                .OrderBy(item => item.Product.Name)
                .Select(item => new WarehouseStockSnapshotVM
                {
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ProductCode = item.Product.Code,
                    CurrentQuantity = item.CurrentQuantity,
                    MinimumStock = item.Product.MinimumStock
                })
                .ToListAsync(cancellationToken);
        }

        private async Task ValidateIssuanceAsync(WarehouseIssuanceUpsertVM model, List<WarehouseIssuanceItemVM> validItems, CancellationToken cancellationToken)
        {
            if (validItems.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Items), "حداقل یک ردیف معتبر برای خروج ثبت کنید.");
            }

            var exists = await _context.WarehouseIssuances.AnyAsync(item => item.IssuanceNumber == model.IssuanceNumber.Trim(), cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.IssuanceNumber), "شماره خروج تکراری است.");
            }

            if (!model.EmployerId.HasValue || model.EmployerId.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.EmployerId), "انتخاب کارفرما الزامی است.");
            }

            if (model.EmployerId.HasValue)
            {
                var employerExists = await _financeContext.Employers
                    .AsNoTracking()
                    .AnyAsync(item => item.Id == model.EmployerId.Value && item.IsActive, cancellationToken);
                if (!employerExists)
                {
                    ModelState.AddModelError(nameof(model.EmployerId), "کارفرمای انتخابی معتبر نیست.");
                }
            }

            await ValidateWarehouseIsOpenAsync(model.WarehouseId, cancellationToken);
            await ValidateProductsExistenceAsync(validItems.Select(item => item.ProductId), cancellationToken);

            if (!ModelState.IsValid)
            {
                return;
            }

            var grouped = validItems
                .GroupBy(item => item.ProductId)
                .Select(group => new { ProductId = group.Key, Quantity = group.Sum(row => row.Quantity) })
                .ToList();

            var productIds = grouped.Select(item => item.ProductId).ToList();
            var stocks = await _context.InventoryStocks
                .AsNoTracking()
                .Where(item => item.WarehouseId == model.WarehouseId && productIds.Contains(item.ProductId))
                .ToDictionaryAsync(item => item.ProductId, item => item.CurrentQuantity, cancellationToken);

            var productNames = await _context.Products
                .AsNoTracking()
                .Where(item => productIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

            foreach (var item in grouped)
            {
                var currentQty = stocks.TryGetValue(item.ProductId, out var qty) ? qty : 0;
                if (currentQty < item.Quantity)
                {
                    var productName = productNames.TryGetValue(item.ProductId, out var name) ? name : $"#{item.ProductId}";
                    ModelState.AddModelError(nameof(model.Items), $"موجودی کالا {productName} کافی نیست. موجودی: {currentQty:N3}");
                }
            }
        }

        private async Task ValidateWarehouseIsOpenAsync(int warehouseId, CancellationToken cancellationToken)
        {
            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == warehouseId, cancellationToken);

            if (warehouse == null)
            {
                ModelState.AddModelError(string.Empty, "انبار انتخاب‌شده معتبر نیست.");
                return;
            }

            if (!warehouse.IsActive)
            {
                ModelState.AddModelError(string.Empty, "انبار انتخاب‌شده غیرفعال است.");
            }

            if (warehouse.IsClosed)
            {
                ModelState.AddModelError(string.Empty, "انبار انتخاب‌شده بسته است.");
            }
        }

        private async Task ValidateProductsExistenceAsync(IEnumerable<int> productIds, CancellationToken cancellationToken)
        {
            var ids = productIds.Where(item => item > 0).Distinct().ToList();
            if (!ids.Any())
            {
                return;
            }

            var validIds = await _context.Products
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted && ids.Contains(item.Id))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);

            var invalidIds = ids.Except(validIds).ToList();
            if (invalidIds.Count > 0)
            {
                ModelState.AddModelError(string.Empty, "برخی کالاهای انتخاب شده معتبر یا فعال نیستند.");
            }
        }

        private async Task<(bool Success, string? Message)> ApplyIssuanceToStockAsync(int warehouseId, List<WarehouseIssuanceItemVM> items, string documentType, string documentId, string? note, CancellationToken cancellationToken)
        {
            var grouped = items
                .GroupBy(item => item.ProductId)
                .Select(group => new { ProductId = group.Key, Quantity = group.Sum(row => row.Quantity) })
                .ToList();

            var productIds = grouped.Select(item => item.ProductId).ToList();
            var stocks = await _context.InventoryStocks
                .Where(item => item.WarehouseId == warehouseId && productIds.Contains(item.ProductId))
                .ToListAsync(cancellationToken);

            var productNames = await _context.Products
                .AsNoTracking()
                .Where(item => productIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

            foreach (var item in grouped)
            {
                var stock = stocks.FirstOrDefault(row => row.ProductId == item.ProductId);
                var current = stock?.CurrentQuantity ?? 0;
                if (current < item.Quantity)
                {
                    var productName = productNames.TryGetValue(item.ProductId, out var name) ? name : $"#{item.ProductId}";
                    return (false, $"موجودی کالا {productName} کافی نیست. موجودی: {current:N3}");
                }
            }

            var now = DateTime.Now;
            foreach (var item in grouped)
            {
                var stock = stocks.First(row => row.ProductId == item.ProductId);
                stock.CurrentQuantity -= item.Quantity;
                stock.UpdatedAt = now;

                _context.InventoryMovementLedgers.Add(new InventoryMovementLedger
                {
                    DocumentType = documentType,
                    DocumentId = documentId,
                    WarehouseId = warehouseId,
                    ProductId = item.ProductId,
                    QuantityIn = 0,
                    QuantityOut = item.Quantity,
                    BalanceAfter = stock.CurrentQuantity,
                    CreatedByUserId = CurrentUserId,
                    CreatedAt = now,
                    Notes = note
                });
            }

            return (true, null);
        }

        private async Task<(bool Success, string? Message)> ApplyIssuanceToStockWithRetryAsync(int warehouseId, List<WarehouseIssuanceItemVM> items, string documentType, string documentId, string? note, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var result = await ApplyIssuanceToStockAsync(warehouseId, items, documentType, documentId, note, cancellationToken);
                if (!result.Success)
                {
                    return result;
                }

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return (true, null);
                }
                catch (DbUpdateConcurrencyException)
                {
                    foreach (var entry in _context.ChangeTracker.Entries<InventoryStock>())
                    {
                        await entry.ReloadAsync(cancellationToken);
                    }

                    if (attempt == maxAttempts)
                    {
                        return (false, "تداخل همزمان در کسر موجودی رخ داد. لطفاً دوباره تلاش کنید.");
                    }
                }
            }

            return (false, "کسر موجودی ناموفق بود.");
        }

        private async Task PopulateProductOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
        {
            options.Clear();
            var items = await _context.Products
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.Name)
                .Select(item => new SelectListItem
                {
                    Value = item.Id.ToString(),
                    Text = $"{item.Name} ({item.Code})"
                })
                .ToListAsync(cancellationToken);

            options.AddRange(items);
        }

        private async Task PopulateWarehouseOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
        {
            options.Clear();
            var items = await _context.Warehouses
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsClosed)
                .OrderBy(item => item.Name)
                .Select(item => new SelectListItem
                {
                    Value = item.Id.ToString(),
                    Text = $"{item.Name} ({item.Code})"
                })
                .ToListAsync(cancellationToken);

            options.AddRange(items);
        }

        private async Task PopulateEmployerOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
        {
            options.Clear();
            var items = await _financeContext.Employers
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Name)
                .Select(item => new SelectListItem
                {
                    Value = item.Id.ToString(),
                    Text = item.Name
                })
                .ToListAsync(cancellationToken);

            options.AddRange(items);
        }

        private async Task<List<SelectListItem>> BuildEmployerOptionsAsync(int? selectedId, CancellationToken cancellationToken)
        {
            var options = new List<SelectListItem>();
            await PopulateEmployerOptionsAsync(options, cancellationToken);
            options.Insert(0, new SelectListItem { Value = string.Empty, Text = "همه کارفرماها", Selected = !selectedId.HasValue });
            if (selectedId.HasValue)
            {
                foreach (var option in options)
                {
                    option.Selected = option.Value == selectedId.Value.ToString();
                }
            }

            return options;
        }

        private async Task<List<SelectListItem>> BuildWarehouseOptionsAsync(int? selectedId, CancellationToken cancellationToken, bool includeClosed = true)
        {
            var query = _context.Warehouses.AsNoTracking().AsQueryable();

            if (!includeClosed)
            {
                query = query.Where(item => item.IsActive && !item.IsClosed);
            }

            return await query
                .OrderBy(item => item.Name)
                .Select(item => new SelectListItem
                {
                    Value = item.Id.ToString(),
                    Text = $"{item.Name} ({item.Code})",
                    Selected = selectedId.HasValue && selectedId.Value == item.Id
                })
                .ToListAsync(cancellationToken);
        }

        private async Task StartWarehouseDocumentWorkflowAsync(
            string documentType,
            int documentId,
            int warehouseId,
            string startedByUserId,
            CancellationToken cancellationToken)
        {
            var managerUserId = await _context.Warehouses
                .AsNoTracking()
                .Where(item => item.Id == warehouseId)
                .Select(item => item.ManagerUserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(managerUserId))
            {
                return;
            }

            await _workflowService.StartDirectAssignmentAsync(
                documentType,
                documentId,
                startedByUserId,
                managerUserId,
                DateTimeOffset.UtcNow.AddDays(2),
                cancellationToken);
        }

        private async Task RecordWarehouseDecisionAsync(
            string documentType,
            int documentId,
            string decision,
            string? comment,
            CancellationToken cancellationToken)
        {
            var currentUserId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return;
            }

            await _workflowService.RecordDecisionAsync(
                documentType,
                documentId,
                1,
                currentUserId,
                decision,
                comment,
                cancellationToken);
        }

        private async Task<string> BuildNextIssuanceNumberAsync(CancellationToken cancellationToken)
        {
            var count = await _context.WarehouseIssuances.CountAsync(cancellationToken);
            return $"WI-{count + 1:00000}";
        }

        private async Task<string> ResolveEmployerNameAsync(int? employerId, string? fallbackName, CancellationToken cancellationToken)
        {
            if (employerId.HasValue)
            {
                var employerName = await _financeContext.Employers
                    .AsNoTracking()
                    .Where(item => item.Id == employerId.Value)
                    .Select(item => item.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(employerName))
                {
                    return employerName;
                }
            }

            return string.IsNullOrWhiteSpace(fallbackName) ? "نامشخص" : fallbackName.Trim();
        }

        private async Task<List<WarehouseAuditEntryVM>> BuildAuditEntriesAsync(string tableName, string entityId, CancellationToken cancellationToken)
        {
            var logs = await _context.AuditLogs
                .AsNoTracking()
                .Where(item => item.TableName == tableName)
                .OrderByDescending(item => item.DateTime)
                .ToListAsync(cancellationToken);

            var userIds = logs
                .Where(item => !string.IsNullOrWhiteSpace(item.UserId))
                .Select(item => item.UserId!)
                .Distinct()
                .ToList();

            var userNames = userIds.Count == 0
                ? new Dictionary<string, string>()
                : await _identityContext.Users
                    .AsNoTracking()
                    .Where(item => userIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, item => item.FullName ?? item.UserName ?? "کاربر", cancellationToken);

            var result = new List<WarehouseAuditEntryVM>();

            foreach (var log in logs)
            {
                if (!MatchesEntity(log.OldValues, entityId) && !MatchesEntity(log.NewValues, entityId))
                {
                    continue;
                }

                result.Add(new WarehouseAuditEntryVM
                {
                    Action = log.Action,
                    Caption = BuildAuditCaption(log),
                    OccurredAt = log.DateTime,
                    ActorName = log.UserId != null && userNames.TryGetValue(log.UserId, out var displayName) ? displayName : null
                });
            }

            return result;
        }

        private async Task<List<WarehouseMovementEntryVM>> BuildMovementEntriesAsync(string documentType, string documentId, CancellationToken cancellationToken)
        {
            var rows = await _context.InventoryMovementLedgers
                .AsNoTracking()
                .Include(item => item.Product)
                .Include(item => item.Warehouse)
                .Where(item => item.DocumentType == documentType && item.DocumentId == documentId)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new WarehouseMovementEntryVM
                {
                    ProductName = item.Product.Name,
                    ProductCode = item.Product.Code,
                    WarehouseName = item.Warehouse.Name,
                    QuantityIn = item.QuantityIn,
                    QuantityOut = item.QuantityOut,
                    BalanceAfter = item.BalanceAfter,
                    CreatedAt = item.CreatedAt,
                    ActorName = item.CreatedByUserId
                })
                .ToListAsync(cancellationToken);

            var userIds = rows
                .Where(item => !string.IsNullOrWhiteSpace(item.ActorName))
                .Select(item => item.ActorName!)
                .Distinct()
                .ToList();

            if (userIds.Count == 0)
            {
                return rows;
            }

            var userNames = await _identityContext.Users
                .AsNoTracking()
                .Where(item => userIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.FullName ?? item.UserName ?? "کاربر", cancellationToken);

            foreach (var row in rows)
            {
                if (row.ActorName != null && userNames.TryGetValue(row.ActorName, out var displayName))
                {
                    row.ActorName = displayName;
                }
            }

            return rows;
        }

        private static bool MatchesEntity(string? json, string entityId)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    var value = property.Value.ToString();
                    if (string.Equals(value, entityId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return json.Contains(entityId, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static string BuildAuditCaption(AuditLog log)
        {
            var columns = string.IsNullOrWhiteSpace(log.AffectedColumns) ? "ثبت رویداد" : log.AffectedColumns;
            return $"{log.Action} - {columns}";
        }

        private static string GetTodayShamsi()
        {
            var persianCalendar = new System.Globalization.PersianCalendar();
            var now = DateTime.Now;
            return $"{persianCalendar.GetYear(now):0000}/{persianCalendar.GetMonth(now):00}/{persianCalendar.GetDayOfMonth(now):00}";
        }

        private Task WriteAuditLogAsync(
            string action,
            string entityName,
            string entityId,
            object? oldValues,
            object? newValues,
            CancellationToken cancellationToken)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Action = action,
                TableName = entityName,
                DateTime = DateTimeOffset.UtcNow,
                OldValues = oldValues == null ? null : JsonSerializer.Serialize(oldValues),
                NewValues = newValues == null ? null : JsonSerializer.Serialize(newValues),
                AffectedColumns = oldValues == null && newValues == null ? null : "ManualWarehouseEvent",
                UserId = CurrentUserId,
                IsSensitive = string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase)
            });

            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
