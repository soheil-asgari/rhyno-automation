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
public class WarehouseInventoryController : Controller
    {
        private readonly InventoryDbContext _context;
        private readonly IdentityDbContext _identityContext;
        private readonly IAuthorizationFacade _authorizationFacade;
        private const int DefaultWarehouseId = 1;

        public WarehouseInventoryController(
            InventoryDbContext context,
            IdentityDbContext identityContext,
            IAuthorizationFacade authorizationFacade)
        {
            _context = context;
            _identityContext = identityContext;
            _authorizationFacade = authorizationFacade;
        }

        private string? CurrentUserId => User?.Identity?.IsAuthenticated == true ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;

        [HttpGet]
        public async Task<IActionResult> Stock(InventoryStockIndexVM filter, CancellationToken cancellationToken)
        {
            var query = _context.InventoryStocks
                .AsNoTracking()
                .Include(item => item.Product)
                .Include(item => item.Warehouse)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                query = query.Where(item => item.Product.Name.Contains(term) || item.Product.Code.Contains(term));
            }

            if (filter.WarehouseId.HasValue)
            {
                query = query.Where(item => item.WarehouseId == filter.WarehouseId.Value);
            }

            if (filter.CriticalOnly)
            {
                query = query.Where(item => item.CurrentQuantity <= item.Product.MinimumStock);
            }

            var baseRows = await query
                .OrderBy(item => item.Warehouse.Name)
                .ThenBy(item => item.Product.Name)
                .Select(item => new InventoryStockRowVM
                {
                    ProductId = item.ProductId,
                    ProductCode = item.Product.Code,
                    ProductName = item.Product.Name,
                    Unit = item.Product.Unit,
                    WarehouseId = item.WarehouseId,
                    WarehouseName = item.Warehouse.Name,
                    CurrentQuantity = item.CurrentQuantity,
                    MinimumStock = item.Product.MinimumStock,
                    UpdatedAt = item.UpdatedAt,
                    LastPurchasePrice = item.Product.LastPurchasePrice ?? 0,
                    AverageCost = 0,
                    InventoryValue = 0,
                    WeightedCost = 0
                })
                .ToListAsync(cancellationToken);

            var warehouseIds = baseRows.Select(item => item.WarehouseId).Distinct().ToList();
            var productIds = baseRows.Select(item => item.ProductId).Distinct().ToList();

            var receiptSums = await _context.WarehouseReceiptItems
                .AsNoTracking()
                .Where(item => warehouseIds.Contains(item.WarehouseReceipt.WarehouseId) && productIds.Contains(item.ProductId))
                .GroupBy(item => new { item.WarehouseReceipt.WarehouseId, item.ProductId })
                .Select(group => new
                {
                    group.Key.WarehouseId,
                    group.Key.ProductId,
                    Quantity = group.Sum(item => item.Quantity),
                    Amount = group.Sum(item => item.Quantity * item.UnitPrice)
                })
                .ToDictionaryAsync(item => $"{item.WarehouseId}:{item.ProductId}", item => new { item.Quantity, item.Amount }, cancellationToken);

            var issuanceSums = await _context.WarehouseIssuanceItems
                .AsNoTracking()
                .Where(item => warehouseIds.Contains(item.WarehouseIssuance.WarehouseId) && productIds.Contains(item.ProductId))
                .GroupBy(item => new { item.WarehouseIssuance.WarehouseId, item.ProductId })
                .Select(group => new
                {
                    group.Key.WarehouseId,
                    group.Key.ProductId,
                    Quantity = group.Sum(item => item.Quantity)
                })
                .ToDictionaryAsync(item => $"{item.WarehouseId}:{item.ProductId}", item => item.Quantity, cancellationToken);

            foreach (var row in baseRows)
            {
                var key = $"{row.WarehouseId}:{row.ProductId}";
                var receiptData = receiptSums.TryGetValue(key, out var value) ? value : null;
                row.TotalInput = receiptData?.Quantity ?? 0;
                row.TotalOutput = issuanceSums.TryGetValue(key, out var outputQty) ? outputQty : 0;
                row.AverageCost = row.TotalInput > 0 ? (receiptData?.Amount ?? 0) / row.TotalInput : row.LastPurchasePrice;
                row.WeightedCost = row.AverageCost;
                row.InventoryValue = row.CurrentQuantity * row.WeightedCost;
            }

            filter.Items = baseRows;

            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(filter.WarehouseId, cancellationToken);
            return View("~/Views/Warehouse/Stock.cshtml", filter);
        }

        [HttpGet]
        public async Task<IActionResult> Countings(string? searchTerm, string? status, int? warehouseId, string? dateFrom, string? dateTo, CancellationToken cancellationToken)
        {
            var query = _context.InventoryCountings
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item => item.DocumentNumber.Contains(term) || item.Status.Contains(term));
            }

            if (warehouseId.HasValue)
            {
                query = query.Where(item => item.WarehouseId == warehouseId.Value);
            }

            var items = await query
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(status))
            {
                items = items.Where(item => WorkflowStatus.Normalize(item.Status) == WorkflowStatus.Normalize(status)).ToList();
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
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(warehouseId, cancellationToken);
            return View("~/Views/Warehouse/Countings.cshtml", items);
        }

        [HttpGet]
        public async Task<IActionResult> CountingDetails(int id, CancellationToken cancellationToken)
        {
            var counting = await _context.InventoryCountings
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (counting == null)
            {
                return NotFound();
            }

            var model = new WarehouseCountingDetailsVM
            {
                Counting = counting,
                AuditEntries = await BuildAuditEntriesAsync("InventoryCounting", id.ToString(), cancellationToken),
                MovementEntries = await BuildMovementEntriesAsync("InventoryCounting", id.ToString(), cancellationToken)
            };

            return View("~/Views/Warehouse/CountingDetails.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateCounting(CancellationToken cancellationToken)
        {
            var model = new InventoryCountingUpsertVM
            {
                DateShamsi = GetTodayShamsi(),
                DocumentNumber = await BuildNextCountingNumberAsync(cancellationToken),
                Status = "Draft",
                WarehouseId = DefaultWarehouseId,
                Items = new List<InventoryCountingItemVM> { new() }
            };

            await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
            await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
            ViewBag.StockSnapshotJson = JsonSerializer.Serialize(await BuildWarehouseStockSnapshotAsync(model.WarehouseId, cancellationToken), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return View("~/Views/Warehouse/CreateCounting.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCounting(InventoryCountingUpsertVM model, CancellationToken cancellationToken)
        {
            var validItems = model.Items.Where(item => item.ProductId > 0).ToList();
            await ValidateCountingAsync(model, validItems, cancellationToken);

            if (!ModelState.IsValid)
            {
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
                ViewBag.StockSnapshotJson = JsonSerializer.Serialize(await BuildWarehouseStockSnapshotAsync(model.WarehouseId, cancellationToken), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return View("~/Views/Warehouse/CreateCounting.cshtml", model);
            }

            var entity = new InventoryCounting
            {
                DocumentNumber = model.DocumentNumber.Trim(),
                DateShamsi = model.DateShamsi.Trim(),
                WarehouseId = model.WarehouseId,
                Notes = model.Notes?.Trim(),
                Status = model.Status,
                CreatedAt = DateTime.Now,
                Items = validItems.Select(item => new InventoryCountingItem
                {
                    ProductId = item.ProductId,
                    SystemQuantity = item.SystemQuantity,
                    PhysicalQuantity = item.PhysicalQuantity,
                    DiscrepancyQuantity = item.PhysicalQuantity - item.SystemQuantity
                }).ToList()
            };

            _context.InventoryCountings.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Create",
                "InventoryCounting",
                entity.Id.ToString(),
                null,
                new
                {
                    entity.DocumentNumber,
                    entity.DateShamsi,
                    entity.WarehouseId,
                    entity.Status,
                    ItemCount = entity.Items.Count
                },
                cancellationToken);

            return RedirectToAction(nameof(CountingDetails), new { id = entity.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Warehouse.Approve")]
        public async Task<IActionResult> ApproveCounting(int id, CancellationToken cancellationToken)
        {
            var counting = await _context.InventoryCountings
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (counting == null)
            {
                return NotFound();
            }

            if (counting.Status == "Approved")
            {
                return RedirectToAction(nameof(CountingDetails), new { id });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var itemVMs = counting.Items.Select(item => new InventoryCountingItemVM
            {
                ProductId = item.ProductId,
                SystemQuantity = item.SystemQuantity,
                PhysicalQuantity = item.PhysicalQuantity
            }).ToList();

            await ApplyCountingApprovalToStockAsync(counting.WarehouseId, itemVMs, "InventoryCounting", counting.Id.ToString(), counting.DocumentNumber, cancellationToken);

            counting.Status = "Approved";
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync("Edit", "InventoryCounting", counting.Id.ToString(), new { Status = "Draft" }, new { Status = "Approved" }, cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انبارگردانی با موفقیت تایید و اعمال شد.";
            return RedirectToAction(nameof(CountingDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Closing(CancellationToken cancellationToken)
        {
            var model = new WarehouseClosingRequestVM
            {
                ClosingDateShamsi = GetTodayShamsi(),
                ClosingYear = GetCurrentShamsiYear(),
                WarehouseId = DefaultWarehouseId
            };

            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(model.WarehouseId, cancellationToken, includeClosed: false);
            return View("~/Views/Warehouse/Closing.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Closing(WarehouseClosingRequestVM model, CancellationToken cancellationToken)
        {
            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(item => item.Id == model.WarehouseId, cancellationToken);
            if (warehouse == null)
            {
                return NotFound();
            }

            if (warehouse.IsClosed)
            {
                ModelState.AddModelError(string.Empty, "انبار انتخاب‌شده قبلاً بسته شده است.");
            }

            var preflights = await BuildWarehouseClosingPreflightAsync(model.WarehouseId, cancellationToken);
            if (preflights.Any(item => item.IsBlocking))
            {
                ModelState.AddModelError(string.Empty, "چک‌های قبل از بستن سال کامل نیست. موارد قرمز را برطرف کنید.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(model.WarehouseId, cancellationToken, includeClosed: false);
                return View("~/Views/Warehouse/Closing.cshtml", model);
            }

            var stocks = await _context.InventoryStocks
                .Where(item => item.WarehouseId == model.WarehouseId)
                .ToListAsync(cancellationToken);

            var now = DateTime.Now;
            var openingYear = model.ClosingYear + 1;
            var wasClosed = warehouse!.IsClosed;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var closing = new WarehouseClosing
            {
                WarehouseId = model.WarehouseId,
                DocumentNumber = await BuildNextClosingNumberAsync(cancellationToken),
                ClosingDateShamsi = model.ClosingDateShamsi.Trim(),
                ClosingYear = model.ClosingYear,
                OpeningYear = openingYear,
                CreatedAt = now,
                Items = stocks.Select(item => new WarehouseClosingItem
                {
                    ProductId = item.ProductId,
                    ClosingQuantity = item.CurrentQuantity,
                    OpeningQuantity = item.CurrentQuantity
                }).ToList()
            };

            _context.WarehouseClosings.Add(closing);
            await _context.SaveChangesAsync(cancellationToken);

            var existingOpeningLedgers = await _context.InventoryOpeningBalanceLedgers
                .Where(item => item.WarehouseId == model.WarehouseId && item.PeriodYear == openingYear)
                .ToListAsync(cancellationToken);

            if (existingOpeningLedgers.Count > 0)
            {
                _context.InventoryOpeningBalanceLedgers.RemoveRange(existingOpeningLedgers);
            }

            var ledgers = stocks.Select(item => new InventoryOpeningBalanceLedger
            {
                WarehouseId = model.WarehouseId,
                ProductId = item.ProductId,
                WarehouseClosingId = closing.Id,
                PeriodYear = openingYear,
                Quantity = item.CurrentQuantity,
                CreatedAt = now
            }).ToList();

            _context.InventoryOpeningBalanceLedgers.AddRange(ledgers);

            warehouse!.IsClosed = true;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Edit",
                "WarehouseClosing",
                closing.Id.ToString(),
                new { IsClosed = wasClosed },
                new
                {
                    closing.DocumentNumber,
                    closing.WarehouseId,
                    closing.ClosingYear,
                    closing.OpeningYear,
                    warehouse.IsClosed
                },
                cancellationToken);

            TempData["WarehouseMessage"] = $"انبار {warehouse.Name} با موفقیت بسته و موجودی افتتاحیه سال {openingYear} ایجاد شد.";
            return RedirectToAction("Warehouses", "Warehouse");
        }

        private async Task<List<WarehouseClosingPreflightItemVM>> BuildWarehouseClosingPreflightAsync(int warehouseId, CancellationToken cancellationToken)
        {
            var items = new List<WarehouseClosingPreflightItemVM>();

            var pendingTransfers = await _context.InventoryTransferRequests
                .AsNoTracking()
                .CountAsync(item => item.SourceWarehouseId == warehouseId && item.Status != WorkflowStatus.Approved && item.Status != WorkflowStatus.Canceled, cancellationToken);
            items.Add(new WarehouseClosingPreflightItemVM
            {
                Key = "pending_transfers",
                Title = "درخواست‌های انتقال باز",
                Detail = pendingTransfers > 0 ? $"{pendingTransfers} درخواست انتقال هنوز تایید نشده است." : "مورد باز وجود ندارد.",
                IsBlocking = pendingTransfers > 0
            });

            var pendingCountings = await _context.InventoryCountings
                .AsNoTracking()
                .CountAsync(item => item.WarehouseId == warehouseId && item.Status != WorkflowStatus.Approved && item.Status != WorkflowStatus.Canceled, cancellationToken);
            items.Add(new WarehouseClosingPreflightItemVM
            {
                Key = "pending_countings",
                Title = "انبارگردانی‌های ناتمام",
                Detail = pendingCountings > 0 ? $"{pendingCountings} انبارگردانی هنوز تایید نشده است." : "مورد باز وجود ندارد.",
                IsBlocking = pendingCountings > 0
            });

            var pendingReceipts = await _context.WarehouseReceipts
                .AsNoTracking()
                .CountAsync(item => item.WarehouseId == warehouseId && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Canceled, cancellationToken);
            items.Add(new WarehouseClosingPreflightItemVM
            {
                Key = "pending_receipts",
                Title = "رسیدهای ناتمام",
                Detail = pendingReceipts > 0 ? $"{pendingReceipts} رسید هنوز در انتظار بررسی است." : "مورد باز وجود ندارد.",
                IsBlocking = pendingReceipts > 0
            });

            var pendingIssuances = await _context.WarehouseIssuances
                .AsNoTracking()
                .CountAsync(item => item.WarehouseId == warehouseId && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Canceled, cancellationToken);
            items.Add(new WarehouseClosingPreflightItemVM
            {
                Key = "pending_issuances",
                Title = "خروج‌های ناتمام",
                Detail = pendingIssuances > 0 ? $"{pendingIssuances} خروج هنوز در انتظار بررسی است." : "مورد باز وجود ندارد.",
                IsBlocking = pendingIssuances > 0
            });

            var negativeStocks = await _context.InventoryStocks
                .AsNoTracking()
                .CountAsync(item => item.WarehouseId == warehouseId && item.CurrentQuantity < 0, cancellationToken);
            items.Add(new WarehouseClosingPreflightItemVM
            {
                Key = "negative_stocks",
                Title = "موجونی منفی",
                Detail = negativeStocks > 0 ? $"{negativeStocks} قلم موجودی منفی ثبت شده است." : "موجودی منفی یافت نشد.",
                IsBlocking = negativeStocks > 0
            });

            var lastUpdate = await _context.InventoryMovementLedgers
                .AsNoTracking()
                .Where(item => item.WarehouseId == warehouseId)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            items.Add(new WarehouseClosingPreflightItemVM
            {
                Key = "last_update",
                Title = "آخرین بروزرسانی",
                Detail = lastUpdate == default ? "هنوز حرکت ثبت نشده است." : lastUpdate.ToString("yyyy/MM/dd HH:mm"),
                IsBlocking = false
            });

            return items;
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

        private async Task ValidateCountingAsync(InventoryCountingUpsertVM model, List<InventoryCountingItemVM> validItems, CancellationToken cancellationToken)
        {
            if (validItems.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Items), "حداقل یک ردیف معتبر برای انبارگردانی ثبت کنید.");
            }

            var exists = await _context.InventoryCountings.AnyAsync(item => item.DocumentNumber == model.DocumentNumber.Trim(), cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.DocumentNumber), "شماره سند تکراری است.");
            }

            if (model.Status != "Draft" && model.Status != "Approved")
            {
                ModelState.AddModelError(nameof(model.Status), "وضعیت باید Draft یا Approved باشد.");
            }

            await ValidateWarehouseIsOpenAsync(model.WarehouseId, cancellationToken);
            await ValidateProductsExistenceAsync(validItems.Select(item => item.ProductId), cancellationToken);
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

        private async Task ApplyCountingApprovalToStockAsync(int warehouseId, List<InventoryCountingItemVM> items, string documentType, string documentId, string? note, CancellationToken cancellationToken)
        {
            var productIds = items.Select(item => item.ProductId).Distinct().ToList();
            var stocks = await _context.InventoryStocks
                .Where(item => item.WarehouseId == warehouseId && productIds.Contains(item.ProductId))
                .ToListAsync(cancellationToken);

            var now = DateTime.Now;

            foreach (var item in items)
            {
                item.DiscrepancyQuantity = item.PhysicalQuantity - item.SystemQuantity;

                var stock = stocks.FirstOrDefault(row => row.ProductId == item.ProductId);
                if (stock == null)
                {
                    stock = new InventoryStock
                    {
                        ProductId = item.ProductId,
                        WarehouseId = warehouseId,
                        CurrentQuantity = 0,
                        UpdatedAt = now
                    };
                    _context.InventoryStocks.Add(stock);
                }

                stock.CurrentQuantity = item.PhysicalQuantity;
                stock.UpdatedAt = now;

                var diff = item.PhysicalQuantity - item.SystemQuantity;
                _context.InventoryMovementLedgers.Add(new InventoryMovementLedger
                {
                    DocumentType = documentType,
                    DocumentId = documentId,
                    WarehouseId = warehouseId,
                    ProductId = item.ProductId,
                    QuantityIn = diff > 0 ? diff : 0,
                    QuantityOut = diff < 0 ? Math.Abs(diff) : 0,
                    BalanceAfter = stock.CurrentQuantity,
                    CreatedByUserId = CurrentUserId,
                    CreatedAt = now,
                    Notes = note
                });
            }
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

        private async Task<string> BuildNextCountingNumberAsync(CancellationToken cancellationToken)
        {
            var count = await _context.InventoryCountings.CountAsync(cancellationToken);
            return $"IC-{count + 1:00000}";
        }

        private async Task<string> BuildNextClosingNumberAsync(CancellationToken cancellationToken)
        {
            var count = await _context.WarehouseClosings.CountAsync(cancellationToken);
            return $"WC-{count + 1:00000}";
        }

        private static int GetCurrentShamsiYear()
        {
            var persianCalendar = new System.Globalization.PersianCalendar();
            return persianCalendar.GetYear(DateTime.Now);
        }

        private static string GetTodayShamsi()
        {
            var persianCalendar = new System.Globalization.PersianCalendar();
            var now = DateTime.Now;
            return $"{persianCalendar.GetYear(now):0000}/{persianCalendar.GetMonth(now):00}/{persianCalendar.GetDayOfMonth(now):00}";
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
