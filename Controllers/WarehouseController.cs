using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using OfficeAutomation.Data;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [RequireAccessArea("Warehouse")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationFacade _authorizationFacade;
        private const int DefaultWarehouseId = 1;

        public WarehouseController(ApplicationDbContext context, IAuthorizationFacade authorizationFacade)
        {
            _context = context;
            _authorizationFacade = authorizationFacade;
        }

        private string? CurrentUserId => User?.Identity?.IsAuthenticated == true ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var pendingTransferRows = await _context.InventoryTransferRequests
                .AsNoTracking()
                .Include(item => item.Product)
                .Include(item => item.SourceWarehouse)
                .Include(item => item.DestinationWarehouse)
                .Where(item => item.Status != WorkflowStatus.Approved && item.Status != WorkflowStatus.Canceled)
                .OrderByDescending(item => item.CreatedAt)
                .Take(6)
                .Select(item => new WarehousePendingTransferVM
                {
                    Id = item.Id,
                    ProductName = item.Product != null ? item.Product.Name : string.Empty,
                    SourceWarehouseName = item.SourceWarehouse != null ? item.SourceWarehouse.Name : string.Empty,
                    DestinationWarehouseName = item.DestinationWarehouse != null ? item.DestinationWarehouse.Name : string.Empty,
                    Quantity = item.Quantity,
                    Status = item.Status
                })
                .ToListAsync(cancellationToken);

            var criticalStocks = await _context.InventoryStocks
                .AsNoTracking()
                .Include(item => item.Product)
                .Include(item => item.Warehouse)
                .Where(item => item.CurrentQuantity <= item.Product.MinimumStock)
                .OrderBy(item => item.CurrentQuantity - item.Product.MinimumStock)
                .ThenBy(item => item.Warehouse.Name)
                .Take(8)
                .Select(item => new WarehouseLowStockItemVM
                {
                    WarehouseId = item.WarehouseId,
                    WarehouseName = item.Warehouse.Name,
                    ProductId = item.ProductId,
                    ProductCode = item.Product.Code,
                    ProductName = item.Product.Name,
                    CurrentQuantity = item.CurrentQuantity,
                    MinimumStock = item.Product.MinimumStock
                })
                .ToListAsync(cancellationToken);

            var recentReceipts = await _context.WarehouseReceipts
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Items)
                .OrderByDescending(item => item.CreatedAt)
                .Take(6)
                .Select(item => new WarehouseRecentDocumentVM
                {
                    Id = item.Id,
                    Number = item.ReceiptNumber,
                    DateShamsi = item.DateShamsi,
                    WarehouseName = item.Warehouse.Name,
                    Status = item.WorkflowStatus,
                    TotalQuantity = item.Items.Sum(row => row.Quantity)
                })
                .ToListAsync(cancellationToken);

            var recentIssuances = await _context.WarehouseIssuances
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Items)
                .OrderByDescending(item => item.CreatedAt)
                .Take(6)
                .Select(item => new WarehouseRecentDocumentVM
                {
                    Id = item.Id,
                    Number = item.IssuanceNumber,
                    DateShamsi = item.DateShamsi,
                    WarehouseName = item.Warehouse.Name,
                    Status = item.WorkflowStatus,
                    TotalQuantity = item.Items.Sum(row => row.Quantity)
                })
                .ToListAsync(cancellationToken);

            var countingIssues = await _context.InventoryCountings
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Items)
                .OrderByDescending(item => item.CreatedAt)
                .Take(6)
                .Select(item => new WarehouseCountingIssueVM
                {
                    Id = item.Id,
                    DocumentNumber = item.DocumentNumber,
                    WarehouseName = item.Warehouse.Name,
                    TotalDiscrepancy = item.Items.Sum(row => row.DiscrepancyQuantity),
                    Status = item.Status
                })
                .ToListAsync(cancellationToken);

            var trendStart = DateTime.Now.Date.AddDays(-6);
            var trendRows = await _context.InventoryMovementLedgers
                .AsNoTracking()
                .Where(item => item.CreatedAt >= trendStart)
                .Select(item => new { item.CreatedAt, item.QuantityIn, item.QuantityOut })
                .ToListAsync(cancellationToken);

            var movementTrends = trendRows
                .GroupBy(item => item.CreatedAt.Date)
                .Select(group => new WarehouseMovementTrendVM
                {
                    Label = group.Key.ToString("MM/dd"),
                    Inputs = group.Sum(item => item.QuantityIn),
                    Outputs = group.Sum(item => item.QuantityOut)
                })
                .OrderBy(item => item.Label)
                .ToList();

            var riskWarehouses = await _context.Warehouses
                .AsNoTracking()
                .Select(item => new WarehouseRiskWarehouseVM
                {
                    WarehouseId = item.Id,
                    WarehouseName = item.Name,
                    OpenDocuments = _context.InventoryTransferRequests.Count(req => req.SourceWarehouseId == item.Id && req.Status != WorkflowStatus.Approved && req.Status != WorkflowStatus.Canceled)
                                      + _context.WarehouseReceipts.Count(req => req.WarehouseId == item.Id && req.WorkflowStatus != WorkflowStatus.Approved && req.WorkflowStatus != WorkflowStatus.Canceled)
                                      + _context.WarehouseIssuances.Count(req => req.WarehouseId == item.Id && req.WorkflowStatus != WorkflowStatus.Approved && req.WorkflowStatus != WorkflowStatus.Canceled),
                    LowStockCount = _context.InventoryStocks.Count(stock => stock.WarehouseId == item.Id && stock.CurrentQuantity <= stock.Product.MinimumStock),
                    NegativeStockCount = _context.InventoryStocks.Count(stock => stock.WarehouseId == item.Id && stock.CurrentQuantity < 0)
                })
                .OrderByDescending(item => item.OpenDocuments + item.LowStockCount + item.NegativeStockCount)
                .Take(5)
                .ToListAsync(cancellationToken);

            var model = new WarehouseDashboardVM
            {
                ProductCount = await _context.Products.CountAsync(item => !item.IsDeleted, cancellationToken),
                WarehouseCount = await _context.Warehouses.CountAsync(item => item.IsActive, cancellationToken),
                ReceiptCount = await _context.WarehouseReceipts.CountAsync(cancellationToken),
                IssuanceCount = await _context.WarehouseIssuances.CountAsync(cancellationToken),
                CountingDraftCount = await _context.InventoryCountings.CountAsync(item => item.Status == "Draft", cancellationToken),
                LowStockCount = await _context.InventoryStocks.CountAsync(item => item.CurrentQuantity <= item.Product.MinimumStock, cancellationToken),
                CriticalStocks = criticalStocks,
                RecentReceipts = recentReceipts,
                RecentIssuances = recentIssuances,
                PendingTransfers = pendingTransferRows,
                RiskWarehouses = riskWarehouses,
                CountingIssues = countingIssues,
                MovementTrends = movementTrends
            };

            return View(model);
        }

        [HttpGet]
        [PermissionAuthorize("Warehouse.View")]
        public async Task<IActionResult> Products(string? searchTerm, CancellationToken cancellationToken)
        {
            var query = _context.Products
                .AsNoTracking()
                .Where(item => !item.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item => item.Name.Contains(term) || item.Code.Contains(term));
            }

            var items = await query
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            ViewBag.SearchTerm = searchTerm;
            return View(items);
        }

        [HttpGet]
        [PermissionAuthorize("Warehouse.Create")]
        public IActionResult CreateProduct()
        {
            return View(new ProductUpsertVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Warehouse.Create")]
        public async Task<IActionResult> CreateProduct(ProductUpsertVM model, CancellationToken cancellationToken)
        {
            await ValidateProductCodeAsync(model.Code, null, cancellationToken);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = new Product
            {
                Code = model.Code.Trim(),
                Name = model.Name.Trim(),
                Unit = model.Unit.Trim(),
                Description = model.Description?.Trim(),
                Category = model.Category?.Trim(),
                Barcode = model.Barcode?.Trim(),
                TechnicalDescription = model.TechnicalDescription?.Trim(),
                IsPurchasable = model.IsPurchasable,
                IsConsumable = model.IsConsumable,
                SecondaryUnit = model.SecondaryUnit?.Trim(),
                ReorderPoint = model.ReorderPoint,
                MaximumStock = model.MaximumStock,
                LastPurchasePrice = model.LastPurchasePrice,
                MinimumStock = model.MinimumStock,
                IsActive = model.IsActive,
                IsDeleted = false,
                CreatedAt = DateTime.Now
            };

            _context.Products.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return RedirectToAction(nameof(Products));
        }

        [HttpGet]
        [PermissionAuthorize("Warehouse.Edit")]
        public async Task<IActionResult> EditProduct(int id, CancellationToken cancellationToken)
        {
            var entity = await _context.Products
                .AsNoTracking()
                .Where(item => !item.IsDeleted)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (entity == null)
            {
                return NotFound();
            }

            return View(new ProductUpsertVM
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                Unit = entity.Unit,
                Description = entity.Description,
                Category = entity.Category,
                Barcode = entity.Barcode,
                TechnicalDescription = entity.TechnicalDescription,
                IsPurchasable = entity.IsPurchasable,
                IsConsumable = entity.IsConsumable,
                SecondaryUnit = entity.SecondaryUnit,
                ReorderPoint = entity.ReorderPoint,
                MaximumStock = entity.MaximumStock,
                LastPurchasePrice = entity.LastPurchasePrice,
                MinimumStock = entity.MinimumStock,
                IsActive = entity.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Warehouse.Edit")]
        public async Task<IActionResult> EditProduct(int id, ProductUpsertVM model, CancellationToken cancellationToken)
        {
            if (model.Id != id)
            {
                return NotFound();
            }

            await ValidateProductCodeAsync(model.Code, id, cancellationToken);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = await _context.Products
                .Where(item => !item.IsDeleted)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            var beforeState = new
            {
                entity.Code,
                entity.Name,
                entity.Unit,
                entity.Description,
                entity.Category,
                entity.Barcode,
                entity.TechnicalDescription,
                entity.IsPurchasable,
                entity.IsConsumable,
                entity.SecondaryUnit,
                entity.ReorderPoint,
                entity.MaximumStock,
                entity.LastPurchasePrice,
                entity.MinimumStock,
                entity.IsActive
            };

            entity.Code = model.Code.Trim();
            entity.Name = model.Name.Trim();
            entity.Unit = model.Unit.Trim();
            entity.Description = model.Description?.Trim();
            entity.Category = model.Category?.Trim();
            entity.Barcode = model.Barcode?.Trim();
            entity.TechnicalDescription = model.TechnicalDescription?.Trim();
            entity.IsPurchasable = model.IsPurchasable;
            entity.IsConsumable = model.IsConsumable;
            entity.SecondaryUnit = model.SecondaryUnit?.Trim();
            entity.ReorderPoint = model.ReorderPoint;
            entity.MaximumStock = model.MaximumStock;
            entity.LastPurchasePrice = model.LastPurchasePrice;
            entity.MinimumStock = model.MinimumStock;
            entity.IsActive = model.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Warehouse.Delete")]
        public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken)
        {
            var entity = await _context.Products.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            var beforeState = new
            {
                entity.Code,
                entity.Name,
                entity.Unit,
                entity.Description,
                entity.IsActive,
                entity.IsDeleted
            };

            var hasTransactions = await _context.WarehouseReceiptItems.AnyAsync(item => item.ProductId == id, cancellationToken)
                                  || await _context.WarehouseIssuanceItems.AnyAsync(item => item.ProductId == id, cancellationToken)
                                  || await _context.InventoryCountingItems.AnyAsync(item => item.ProductId == id, cancellationToken);

            if (hasTransactions)
            {
                entity.IsActive = false;
                entity.IsDeleted = true;
                await _context.SaveChangesAsync(cancellationToken);
                TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
                return RedirectToAction(nameof(Products));
            }

            _context.Products.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            await WriteAuditLogAsync(
                "Delete",
                "Product",
                id.ToString(),
                beforeState,
                null,
                cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Warehouse.Edit")]
        public async Task<IActionResult> ToggleProductStatus(int id, CancellationToken cancellationToken)
        {
            var entity = await _context.Products.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            entity.IsActive = !entity.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Products));
        }

        [HttpGet]
        public async Task<IActionResult> Warehouses(string? searchTerm, CancellationToken cancellationToken)
        {
            var query = _context.Warehouses.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item => item.Name.Contains(term) || item.Code.Contains(term) || (item.Location ?? string.Empty).Contains(term));
            }

            var items = await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
            ViewBag.SearchTerm = searchTerm;
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> CreateWarehouse(CancellationToken cancellationToken)
        {
            var model = new WarehouseUpsertVM();
            await PopulateManagerOptionsAsync(model.ManagerOptions, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWarehouse(WarehouseUpsertVM model, CancellationToken cancellationToken)
        {
            await ValidateWarehouseCodeAsync(model.Code, null, cancellationToken);

            if (!ModelState.IsValid)
            {
                await PopulateManagerOptionsAsync(model.ManagerOptions, cancellationToken);
                return View(model);
            }

            var entity = new Warehouse
            {
                Code = model.Code.Trim(),
                Name = model.Name.Trim(),
                Location = model.Location?.Trim(),
                WarehouseType = model.WarehouseType?.Trim(),
                Capacity = model.Capacity,
                ManagerUserId = string.IsNullOrWhiteSpace(model.ManagerUserId) ? null : model.ManagerUserId,
                IsActive = model.IsActive,
                IsClosed = model.IsClosed,
                ClosingRules = model.ClosingRules?.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.Warehouses.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Warehouses));
        }

        [HttpGet]
        public async Task<IActionResult> EditWarehouse(int id, CancellationToken cancellationToken)
        {
            var entity = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            var model = new WarehouseUpsertVM
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                Location = entity.Location,
                WarehouseType = entity.WarehouseType,
                Capacity = entity.Capacity,
                ManagerUserId = entity.ManagerUserId,
                IsActive = entity.IsActive,
                IsClosed = entity.IsClosed,
                ClosingRules = entity.ClosingRules
            };

            await PopulateManagerOptionsAsync(model.ManagerOptions, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWarehouse(int id, WarehouseUpsertVM model, CancellationToken cancellationToken)
        {
            if (model.Id != id)
            {
                return NotFound();
            }

            await ValidateWarehouseCodeAsync(model.Code, id, cancellationToken);

            if (!ModelState.IsValid)
            {
                await PopulateManagerOptionsAsync(model.ManagerOptions, cancellationToken);
                return View(model);
            }

            var entity = await _context.Warehouses.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            var beforeState = new
            {
                entity.Code,
                entity.Name,
                entity.Location,
                entity.WarehouseType,
                entity.Capacity,
                entity.ManagerUserId,
                entity.IsActive,
                entity.IsClosed,
                entity.ClosingRules
            };

            entity.Code = model.Code.Trim();
            entity.Name = model.Name.Trim();
            entity.Location = model.Location?.Trim();
            entity.WarehouseType = model.WarehouseType?.Trim();
            entity.Capacity = model.Capacity;
            entity.ManagerUserId = string.IsNullOrWhiteSpace(model.ManagerUserId) ? null : model.ManagerUserId;
            entity.IsActive = model.IsActive;
            entity.IsClosed = model.IsClosed;
            entity.ClosingRules = model.ClosingRules?.Trim();

            await _context.SaveChangesAsync(cancellationToken);
            await WriteAuditLogAsync(
                "Edit",
                "Warehouse",
                entity.Id.ToString(),
                beforeState,
                new
                {
                    entity.Code,
                    entity.Name,
                    entity.Location,
                    entity.ManagerUserId,
                    entity.IsActive,
                    entity.IsClosed
                },
                cancellationToken);
            return RedirectToAction(nameof(Warehouses));
        }

        [HttpGet]
        public async Task<IActionResult> Receipts(string? searchTerm, string? status, int? warehouseId, int? vendorId, string? dateFrom, string? dateTo, CancellationToken cancellationToken)
        {
            var query = _context.WarehouseReceipts
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Vendor)
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item => item.ReceiptNumber.Contains(term) || item.SupplierOrSource.Contains(term));
            }

            if (warehouseId.HasValue)
            {
                query = query.Where(item => item.WarehouseId == warehouseId.Value);
            }

            if (vendorId.HasValue)
            {
                query = query.Where(item => item.VendorId == vendorId.Value);
            }

            var items = await query
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(status))
            {
                items = items.Where(item => WorkflowStatus.Normalize(item.WorkflowStatus) == WorkflowStatus.Normalize(status)).ToList();
            }

            if (vendorId.HasValue)
            {
                items = items.Where(item => item.VendorId == vendorId.Value).ToList();
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
            ViewBag.VendorId = vendorId;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(warehouseId, cancellationToken);
            ViewBag.VendorOptions = await BuildVendorOptionsAsync(vendorId, cancellationToken);
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> ReceiptDetails(int id, CancellationToken cancellationToken)
        {
            var receipt = await _context.WarehouseReceipts
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Vendor)
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (receipt == null)
            {
                return NotFound();
            }

            var model = new WarehouseReceiptDetailsVM
            {
                Receipt = receipt,
                TotalQuantity = receipt.Items.Sum(item => item.Quantity),
                TotalAmount = receipt.Items.Sum(item => item.Quantity * item.UnitPrice),
                AuditEntries = await BuildAuditEntriesAsync("WarehouseReceipt", id.ToString(), cancellationToken),
                MovementEntries = await BuildMovementEntriesAsync("WarehouseReceipt", id.ToString(), cancellationToken)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Warehouse.Approve")]
        public async Task<IActionResult> ApproveReceipt(int id, CancellationToken cancellationToken)
        {
            var receipt = await _context.WarehouseReceipts
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (receipt == null)
            {
                return NotFound();
            }

            if (WorkflowStatus.IsApproved(receipt.WorkflowStatus))
            {
                return RedirectToAction(nameof(ReceiptDetails), new { id });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var itemVMs = receipt.Items.Select(item => new WarehouseReceiptItemVM
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList();

            var applyResult = await ApplyReceiptToStockWithRetryAsync(receipt.WarehouseId, itemVMs, "WarehouseReceipt", receipt.Id.ToString(), receipt.ReceiptNumber, cancellationToken);
            if (!applyResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["WarehouseMessage"] = applyResult.Message ?? "ثبت موجودی با خطا مواجه شد.";
                return RedirectToAction(nameof(ReceiptDetails), new { id });
            }

            receipt.WorkflowStatus = WorkflowStatus.Approved;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync("Edit", "WarehouseReceipt", receipt.Id.ToString(), new { Status = receipt.WorkflowStatus }, new { Status = WorkflowStatus.Approved }, cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
            return RedirectToAction(nameof(ReceiptDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectReceipt(int id, CancellationToken cancellationToken)
        {
            var receipt = await _context.WarehouseReceipts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (receipt == null)
            {
                return NotFound();
            }

            receipt.WorkflowStatus = WorkflowStatus.Rejected;
            await _context.SaveChangesAsync(cancellationToken);
            await WriteAuditLogAsync("Edit", "WarehouseReceipt", receipt.Id.ToString(), new { Status = receipt.WorkflowStatus }, new { Status = WorkflowStatus.Rejected }, cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
            return RedirectToAction(nameof(ReceiptDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReceipt(int id, CancellationToken cancellationToken)
        {
            var receipt = await _context.WarehouseReceipts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (receipt == null)
            {
                return NotFound();
            }

            receipt.WorkflowStatus = WorkflowStatus.Canceled;
            await _context.SaveChangesAsync(cancellationToken);
            await WriteAuditLogAsync("Edit", "WarehouseReceipt", receipt.Id.ToString(), new { Status = receipt.WorkflowStatus }, new { Status = WorkflowStatus.Canceled }, cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
            return RedirectToAction(nameof(ReceiptDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReopenReceipt(int id, CancellationToken cancellationToken)
        {
            var receipt = await _context.WarehouseReceipts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (receipt == null)
            {
                return NotFound();
            }

            receipt.WorkflowStatus = WorkflowStatus.Draft;
            await _context.SaveChangesAsync(cancellationToken);
            await WriteAuditLogAsync("Edit", "WarehouseReceipt", receipt.Id.ToString(), new { Status = receipt.WorkflowStatus }, new { Status = WorkflowStatus.Draft }, cancellationToken);
            TempData["WarehouseMessage"] = "درخواست انتقال تایید و انجام شد.";
            return RedirectToAction(nameof(ReceiptDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> CreateReceipt(CancellationToken cancellationToken)
        {
            var model = new WarehouseReceiptUpsertVM
            {
                DateShamsi = GetTodayShamsi(),
                ReceiptNumber = await BuildNextReceiptNumberAsync(cancellationToken),
                WarehouseId = DefaultWarehouseId,
                Items = new List<WarehouseReceiptItemVM> { new() },
                VendorId = null
            };

            await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
            await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
            await PopulateVendorOptionsAsync(model.VendorOptions, cancellationToken);
            ViewBag.StockSnapshotJson = JsonSerializer.Serialize(await BuildWarehouseStockSnapshotAsync(model.WarehouseId, cancellationToken), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReceipt(WarehouseReceiptUpsertVM model, CancellationToken cancellationToken)
        {
            await ValidateReceiptAsync(model, cancellationToken);

            if (!ModelState.IsValid)
            {
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
                await PopulateVendorOptionsAsync(model.VendorOptions, cancellationToken);
                ViewBag.StockSnapshotJson = JsonSerializer.Serialize(await BuildWarehouseStockSnapshotAsync(model.WarehouseId, cancellationToken), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return View(model);
            }

            var validItems = model.Items.Where(item => item.ProductId > 0 && item.Quantity > 0).ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var entity = new WarehouseReceipt
            {
                ReceiptNumber = model.ReceiptNumber.Trim(),
                DateShamsi = model.DateShamsi.Trim(),
                VendorId = model.VendorId,
                SupplierOrSource = (await ResolveVendorNameAsync(model.VendorId, model.SupplierOrSource, cancellationToken)).Trim(),
                Notes = model.Notes?.Trim(),
                WorkflowStatus = model.SaveAsDraft ? WorkflowStatus.Draft : WorkflowStatus.PendingApproval,
                WarehouseId = model.WarehouseId,
                CreatedAt = DateTime.Now,
                Items = validItems.Select(item => new WarehouseReceiptItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList()
            };

            _context.WarehouseReceipts.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Create",
                "WarehouseReceipt",
                entity.Id.ToString(),
                null,
                new
                {
                    entity.ReceiptNumber,
                    entity.DateShamsi,
                    entity.SupplierOrSource,
                    entity.WarehouseId,
                    ItemCount = entity.Items.Count,
                    entity.WorkflowStatus
                },
                cancellationToken);

            return RedirectToAction(nameof(ReceiptDetails), new { id = entity.Id });
        }

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
            return View(items);
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

            return View(model);
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

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

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
            return View(model);
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
                return View(model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

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
            return View(filter);
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
            return View(items);
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

            return View(model);
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
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TransferRequests(string? searchTerm, string? status, int? sourceWarehouseId, int? destinationWarehouseId, int? productId, int? requestedById, string? dateFrom, string? dateTo, CancellationToken cancellationToken)
        {
            var items = await _context.InventoryTransferRequests
                .AsNoTracking()
                .Include(item => item.SourceWarehouse)
                .Include(item => item.DestinationWarehouse)
                .Include(item => item.Product)
                .Include(item => item.RequestedByUser)
                .Include(item => item.ApprovedByUser)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                items = items.Where(item => item.Product?.Name.Contains(term) == true || item.Product?.Code.Contains(term) == true || (item.Description ?? string.Empty).Contains(term)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                items = items.Where(item => WorkflowStatus.Normalize(item.Status) == WorkflowStatus.Normalize(status)).ToList();
            }

            if (sourceWarehouseId.HasValue)
            {
                items = items.Where(item => item.SourceWarehouseId == sourceWarehouseId.Value).ToList();
            }

            if (destinationWarehouseId.HasValue)
            {
                items = items.Where(item => item.DestinationWarehouseId == destinationWarehouseId.Value).ToList();
            }

            if (productId.HasValue)
            {
                items = items.Where(item => item.ProductId == productId.Value).ToList();
            }

            if (requestedById.HasValue)
            {
                items = items.Where(item => item.RequestedByUserId == requestedById.Value.ToString()).ToList();
            }

            if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var fromDate))
            {
                items = items.Where(item => item.CreatedAt >= fromDate.Date).ToList();
            }

            if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var toDate))
            {
                items = items.Where(item => item.CreatedAt <= toDate.Date.AddDays(1).AddTicks(-1)).ToList();
            }

            var currentUserId = CurrentUserId;
            ViewBag.IsManagerApproval = !string.IsNullOrWhiteSpace(currentUserId) &&
                                        await _context.Warehouses
                                            .AsNoTracking()
                                            .AnyAsync(item => item.IsActive && item.ManagerUserId == currentUserId, cancellationToken);
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Status = status;
            ViewBag.SourceWarehouseId = sourceWarehouseId;
            ViewBag.DestinationWarehouseId = destinationWarehouseId;
            ViewBag.ProductId = productId;
            ViewBag.RequestedById = requestedById;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.SourceWarehouseOptions = await BuildWarehouseOptionsAsync(sourceWarehouseId, cancellationToken);
            ViewBag.DestinationWarehouseOptions = await BuildWarehouseOptionsAsync(destinationWarehouseId, cancellationToken);
            ViewBag.ProductOptions = await BuildProductOptionsAsync(productId, cancellationToken);
            ViewBag.RequestedByOptions = await BuildManagerOptionsAsync(requestedById, cancellationToken);
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> TransferRequestDetails(int id, CancellationToken cancellationToken)
        {
            var request = await _context.InventoryTransferRequests
                .AsNoTracking()
                .Include(item => item.SourceWarehouse)
                .Include(item => item.DestinationWarehouse)
                .Include(item => item.Product)
                .Include(item => item.RequestedByUser)
                .Include(item => item.ApprovedByUser)
                .Include(item => item.RejectedByUser)
                .Include(item => item.CanceledByUser)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (request == null)
            {
                return NotFound();
            }

            var currentUserId = CurrentUserId;
            var isSourceManager = !string.IsNullOrWhiteSpace(currentUserId) &&
                                  await _context.Warehouses
                                      .AsNoTracking()
                                      .AnyAsync(item => item.Id == request.SourceWarehouseId && item.ManagerUserId == currentUserId, cancellationToken);
            var hasManagePermission = await _authorizationFacade.HasPermissionAsync("Security.Manage", cancellationToken);
            var canManageTransfer = isSourceManager || hasManagePermission;

            var model = new WarehouseTransferDetailsVM
            {
                Request = request,
                AuditEntries = await BuildAuditEntriesAsync("InventoryTransferRequest", id.ToString(), cancellationToken),
                MovementEntries = await BuildMovementEntriesAsync("InventoryTransferRequest", id.ToString(), cancellationToken)
            };

            ViewBag.CurrentUserId = currentUserId;
            ViewBag.CanManageTransfer = canManageTransfer;
            ViewBag.CanCancelTransfer = canManageTransfer || request.RequestedByUserId == currentUserId;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTransferRequest(CancellationToken cancellationToken)
        {
            var model = new InventoryTransferRequestCreateVM
            {
                Quantity = 1
            };

            await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
            await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTransferRequest(InventoryTransferRequestCreateVM model, CancellationToken cancellationToken)
        {
            if (model.SourceWarehouseId == model.DestinationWarehouseId)
            {
                ModelState.AddModelError(nameof(model.DestinationWarehouseId), "مبدأ و مقصد نمی‌توانند یکسان باشند.");
            }

            await ValidateWarehouseIsOpenAsync(model.SourceWarehouseId, cancellationToken);
            await ValidateWarehouseIsOpenAsync(model.DestinationWarehouseId, cancellationToken);
            await ValidateProductsExistenceAsync(new[] { model.ProductId }, cancellationToken);

            var currentUserId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                return View(model);
            }

            var entity = new InventoryTransferRequest
            {
                SourceWarehouseId = model.SourceWarehouseId,
                DestinationWarehouseId = model.DestinationWarehouseId,
                ProductId = model.ProductId,
                Quantity = model.Quantity,
                Description = model.Description?.Trim(),
                Status = WorkflowStatus.PendingManager,
                RequestedByUserId = currentUserId,
                CreatedAt = DateTime.Now
            };

            _context.InventoryTransferRequests.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Create",
                "InventoryTransferRequest",
                entity.Id.ToString(),
                null,
                new
                {
                    entity.SourceWarehouseId,
                    entity.DestinationWarehouseId,
                    entity.ProductId,
                    entity.Quantity,
                    entity.Status,
                    entity.Description
                },
                cancellationToken);

            TempData["WarehouseMessage"] = "درخواست انتقال ثبت شد و در انتظار بررسی است.";
            return RedirectToAction(nameof(TransferRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Warehouse.Approve")]
        public async Task<IActionResult> ApproveTransferRequest(int id, CancellationToken cancellationToken)
        {
            var currentUserId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Forbid();
            }

            var request = await _context.InventoryTransferRequests
                .Include(item => item.Product)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (request == null)
            {
                return NotFound();
            }

            if (!WorkflowStatus.IsActionPending(request.Status))
            {
                TempData["WarehouseMessage"] = "این درخواست قبلا بررسی شده است.";
                return RedirectToAction(nameof(TransferRequests));
            }

            var isSourceManager = await _context.Warehouses
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.SourceWarehouseId && item.ManagerUserId == currentUserId, cancellationToken);
            if (!isSourceManager && !await _authorizationFacade.HasPermissionAsync("Security.Manage", cancellationToken))
            {
                return Forbid();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var previousStatus = request.Status;

            var issueResult = await ApplyIssuanceToStockWithRetryAsync(
                request.SourceWarehouseId,
                new List<WarehouseIssuanceItemVM> { new() { ProductId = request.ProductId, Quantity = request.Quantity } },
                "InventoryTransferRequest",
                request.Id.ToString(),
                "کسر بابت انتقال",
                cancellationToken);

            if (!issueResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["WarehouseMessage"] = issueResult.Message ?? "موجودی مبدأ کافی نیست.";
                return RedirectToAction(nameof(TransferRequests));
            }

            var receiptResult = await ApplyReceiptToStockWithRetryAsync(
                request.DestinationWarehouseId,
                new List<WarehouseReceiptItemVM> { new() { ProductId = request.ProductId, Quantity = request.Quantity, UnitPrice = 0 } },
                "InventoryTransferRequest",
                request.Id.ToString(),
                "افزایش بابت انتقال",
                cancellationToken);

            if (!receiptResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["WarehouseMessage"] = receiptResult.Message ?? "ثبت موجودی مقصد با خطا مواجه شد.";
                return RedirectToAction(nameof(TransferRequests));
            }

            request.Status = WorkflowStatus.Completed;
            request.ApprovedByUserId = currentUserId;
            request.ApprovedAt = DateTime.Now;
            request.CompletedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Approve",
                "InventoryTransferRequest",
                request.Id.ToString(),
                new { Status = previousStatus },
                new
                {
                    request.Status,
                    request.ApprovedByUserId,
                    request.ApprovedAt,
                    request.CompletedAt
                },
                cancellationToken);

            TempData["WarehouseMessage"] = "درخواست انتقال تایید و اجرا شد.";
            return RedirectToAction(nameof(TransferRequestDetails), new { id = request.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Warehouse.Approve")]
        public async Task<IActionResult> RejectTransferRequest(int id, string? rejectReason, CancellationToken cancellationToken)
        {
            var currentUserId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                TempData["WarehouseMessage"] = "برای رد درخواست باید دلیل ثبت شود.";
                return RedirectToAction(nameof(TransferRequestDetails), new { id });
            }

            var request = await _context.InventoryTransferRequests
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (request == null)
            {
                return NotFound();
            }

            if (!WorkflowStatus.IsActionPending(request.Status))
            {
                TempData["WarehouseMessage"] = "این درخواست قبلا بررسی شده است.";
                return RedirectToAction(nameof(TransferRequests));
            }

            var isSourceManager = await _context.Warehouses
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.SourceWarehouseId && item.ManagerUserId == currentUserId, cancellationToken);
            if (!isSourceManager && !await _authorizationFacade.HasPermissionAsync("Security.Manage", cancellationToken))
            {
                return Forbid();
            }

            var previousStatus = request.Status;
            request.Status = WorkflowStatus.Rejected;
            request.RejectedByUserId = currentUserId;
            request.RejectReason = rejectReason.Trim();
            request.RejectedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Reject",
                "InventoryTransferRequest",
                request.Id.ToString(),
                new { Status = previousStatus },
                new
                {
                    request.Status,
                    request.RejectedByUserId,
                    request.RejectReason,
                    request.RejectedAt
                },
                cancellationToken);

            TempData["WarehouseMessage"] = "درخواست انتقال رد شد.";
            return RedirectToAction(nameof(TransferRequestDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTransferRequest(int id, string? cancelReason, CancellationToken cancellationToken)
        {
            var currentUserId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Forbid();
            }

            var request = await _context.InventoryTransferRequests
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (request == null)
            {
                return NotFound();
            }

            if (!WorkflowStatus.IsActionPending(request.Status))
            {
                TempData["WarehouseMessage"] = "این درخواست دیگر قابل لغو نیست.";
                return RedirectToAction(nameof(TransferRequestDetails), new { id });
            }

            var canCancelAsRequester = request.RequestedByUserId == currentUserId;
            var isSourceManager = await _context.Warehouses
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.SourceWarehouseId && item.ManagerUserId == currentUserId, cancellationToken);
            var hasManagePermission = await _authorizationFacade.HasPermissionAsync("Security.Manage", cancellationToken);
            if (!canCancelAsRequester && !isSourceManager && !hasManagePermission)
            {
                return Forbid();
            }

            var previousStatus = request.Status;
            request.Status = WorkflowStatus.Canceled;
            request.CanceledByUserId = currentUserId;
            request.CancelReason = cancelReason?.Trim();
            request.CanceledAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Cancel",
                "InventoryTransferRequest",
                request.Id.ToString(),
                new { Status = previousStatus },
                new
                {
                    request.Status,
                    request.CanceledByUserId,
                    request.CancelReason,
                    request.CanceledAt
                },
                cancellationToken);

            TempData["WarehouseMessage"] = "درخواست انتقال لغو شد.";
            return RedirectToAction(nameof(TransferRequestDetails), new { id });
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
                return View(model);
            }

            var productIds = validItems.Select(item => item.ProductId).Distinct().ToList();
            var stockMap = await _context.InventoryStocks
                .AsNoTracking()
                .Where(item => item.WarehouseId == model.WarehouseId && productIds.Contains(item.ProductId))
                .ToDictionaryAsync(item => item.ProductId, item => item.CurrentQuantity, cancellationToken);

            foreach (var item in validItems)
            {
                item.SystemQuantity = stockMap.TryGetValue(item.ProductId, out var systemQty) ? systemQty : 0;
                item.DiscrepancyQuantity = item.PhysicalQuantity - item.SystemQuantity;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var entity = new InventoryCounting
            {
                DocumentNumber = model.DocumentNumber.Trim(),
                DateShamsi = model.DateShamsi.Trim(),
                Status = model.Status,
                Notes = model.Notes?.Trim(),
                WarehouseId = model.WarehouseId,
                CreatedAt = DateTime.Now,
                ApprovedAt = model.Status == "Approved" ? DateTime.Now : null,
                Items = validItems.Select(item => new InventoryCountingItem
                {
                    ProductId = item.ProductId,
                    SystemQuantity = item.SystemQuantity,
                    PhysicalQuantity = item.PhysicalQuantity,
                    DiscrepancyQuantity = item.DiscrepancyQuantity
                }).ToList()
            };

            _context.InventoryCountings.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            if (model.Status == "Approved")
            {
                var approvalResult = await ApplyCountingApprovalToStockWithRetryAsync(model.WarehouseId, validItems, "InventoryCounting", entity.Id.ToString(), entity.DocumentNumber, cancellationToken);
                if (!approvalResult.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    ModelState.AddModelError(string.Empty, approvalResult.Message ?? "تایید انبارگردانی به دلیل تداخل همزمان انجام نشد.");
                    await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                    await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
                    return View(model);
                }
            }

            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Create",
                "InventoryCounting",
                entity.Id.ToString(),
                null,
                new
                {
                    entity.DocumentNumber,
                    entity.DateShamsi,
                    entity.Status,
                    entity.WarehouseId,
                    ItemCount = entity.Items.Count
                },
                cancellationToken);

            return RedirectToAction(nameof(CountingDetails), new { id = entity.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                return RedirectToAction(nameof(Countings));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var itemVMs = counting.Items.Select(item => new InventoryCountingItemVM
            {
                ProductId = item.ProductId,
                SystemQuantity = item.SystemQuantity,
                PhysicalQuantity = item.PhysicalQuantity,
                DiscrepancyQuantity = item.DiscrepancyQuantity
            }).ToList();

            var approvalResult = await ApplyCountingApprovalToStockWithRetryAsync(counting.WarehouseId, itemVMs, "InventoryCounting", counting.Id.ToString(), counting.DocumentNumber, cancellationToken);
            if (!approvalResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["WarehouseMessage"] = approvalResult.Message ?? "تایید انبارگردانی به دلیل تداخل همزمان انجام نشد.";
                return RedirectToAction(nameof(Countings));
            }

            counting.Status = "Approved";
            counting.ApprovedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Edit",
                "InventoryCounting",
                counting.Id.ToString(),
                new
                {
                    Status = "Draft"
                },
                new
                {
                    counting.DocumentNumber,
                    counting.Status,
                    counting.ApprovedAt
                },
                cancellationToken);

            return RedirectToAction(nameof(CountingDetails), new { id = counting.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Closing(CancellationToken cancellationToken)
        {
            var model = new WarehouseClosingRequestVM
            {
                WarehouseId = DefaultWarehouseId,
                ClosingDateShamsi = GetTodayShamsi(),
                ClosingYear = GetCurrentShamsiYear()
            };

            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(model.WarehouseId, cancellationToken, includeClosed: false);
            model.PreflightItems = await BuildWarehouseClosingPreflightAsync(model.WarehouseId, cancellationToken);
            model.CanClose = model.PreflightItems.All(item => !item.IsBlocking);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Closing(WarehouseClosingRequestVM model, CancellationToken cancellationToken)
        {
            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(item => item.Id == model.WarehouseId, cancellationToken);
            if (warehouse == null)
            {
                ModelState.AddModelError(nameof(model.WarehouseId), "انبار انتخاب شده معتبر نیست.");
            }
            else
            {
                if (warehouse.IsClosed)
                {
                    ModelState.AddModelError(nameof(model.WarehouseId), "این انبار قبلا بسته شده است.");
                }

                var pendingCountings = await _context.InventoryCountings
                    .AnyAsync(item => item.WarehouseId == model.WarehouseId && item.Status != "Approved", cancellationToken);

                if (pendingCountings)
                {
                    ModelState.AddModelError(string.Empty, "تا زمانی که تمام انبارگردانی‌های انبار تایید نشده باشند، بستن انبار امکان‌پذیر نیست.");
                }
            }

            model.PreflightItems = await BuildWarehouseClosingPreflightAsync(model.WarehouseId, cancellationToken);
            model.CanClose = model.PreflightItems.All(item => !item.IsBlocking);
            if (!model.CanClose)
            {
                ModelState.AddModelError(string.Empty, "چک‌های قبل از بستن سال کامل نیست. موارد قرمز را برطرف کنید.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(model.WarehouseId, cancellationToken, includeClosed: false);
                return View(model);
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
                new
                {
                    IsClosed = wasClosed
                },
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
            return RedirectToAction(nameof(Warehouses));
        }

        private async Task<List<WarehouseClosingPreflightItemVM>> BuildWarehouseClosingPreflightAsync(int warehouseId, CancellationToken cancellationToken)
        {
            var items = new List<WarehouseClosingPreflightItemVM>();

            var pendingTransfers = await _context.InventoryTransferRequests
                .AsNoTracking()
                .CountAsync(item => item.SourceWarehouseId == warehouseId && !WorkflowStatus.IsApproved(item.Status) && item.Status != WorkflowStatus.Canceled, cancellationToken);
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
                .CountAsync(item => item.WarehouseId == warehouseId && !WorkflowStatus.IsApproved(item.WorkflowStatus) && item.WorkflowStatus != WorkflowStatus.Canceled, cancellationToken);
            items.Add(new WarehouseClosingPreflightItemVM
            {
                Key = "pending_receipts",
                Title = "رسیدهای ناتمام",
                Detail = pendingReceipts > 0 ? $"{pendingReceipts} رسید هنوز در انتظار بررسی است." : "مورد باز وجود ندارد.",
                IsBlocking = pendingReceipts > 0
            });

            var pendingIssuances = await _context.WarehouseIssuances
                .AsNoTracking()
                .CountAsync(item => item.WarehouseId == warehouseId && !WorkflowStatus.IsApproved(item.WorkflowStatus) && item.WorkflowStatus != WorkflowStatus.Canceled, cancellationToken);
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
                Title = "موجودی منفی",
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

        private async Task ValidateProductCodeAsync(string? code, int? currentId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            var normalized = code.Trim();
            var exists = await _context.Products.AnyAsync(item => item.Code == normalized && !item.IsDeleted && (!currentId.HasValue || item.Id != currentId.Value), cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(ProductUpsertVM.Code), "کد کالا تکراری است.");
            }
        }

        private async Task ValidateWarehouseCodeAsync(string? code, int? currentId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            var normalized = code.Trim();
            var exists = await _context.Warehouses.AnyAsync(item => item.Code == normalized && (!currentId.HasValue || item.Id != currentId.Value), cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(WarehouseUpsertVM.Code), "کد انبار تکراری است.");
            }
        }

        private async Task ValidateReceiptAsync(WarehouseReceiptUpsertVM model, CancellationToken cancellationToken)
        {
            if (model.Items == null || model.Items.All(item => item.ProductId <= 0 || item.Quantity <= 0))
            {
                ModelState.AddModelError(nameof(model.Items), "حداقل یک ردیف معتبر برای رسید ثبت کنید.");
            }

            var exists = await _context.WarehouseReceipts.AnyAsync(item => item.ReceiptNumber == model.ReceiptNumber.Trim(), cancellationToken);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.ReceiptNumber), "شماره رسید تکراری است.");
            }

            if (!model.VendorId.HasValue || model.VendorId.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.VendorId), "انتخاب تامین‌کننده الزامی است.");
            }

            if (model.VendorId.HasValue)
            {
                var vendorExists = await _context.Vendors
                    .AsNoTracking()
                    .AnyAsync(item => item.Id == model.VendorId.Value && item.IsActive, cancellationToken);
                if (!vendorExists)
                {
                    ModelState.AddModelError(nameof(model.VendorId), "تامین‌کننده انتخابی معتبر نیست.");
                }
            }

            await ValidateWarehouseIsOpenAsync(model.WarehouseId, cancellationToken);
            await ValidateProductsExistenceAsync((model.Items ?? []).Select(item => item.ProductId), cancellationToken);
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
                var employerExists = await _context.Employers
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

        private async Task ApplyReceiptToStockAsync(int warehouseId, List<WarehouseReceiptItemVM> items, string documentType, string documentId, string? note, CancellationToken cancellationToken)
        {
            var grouped = items
                .GroupBy(item => item.ProductId)
                .Select(group => new { ProductId = group.Key, Quantity = group.Sum(row => row.Quantity) })
                .ToList();

            var productIds = grouped.Select(item => item.ProductId).ToList();
            var stocks = await _context.InventoryStocks
                .Where(item => item.WarehouseId == warehouseId && productIds.Contains(item.ProductId))
                .ToListAsync(cancellationToken);

            var now = DateTime.Now;

            foreach (var item in grouped)
            {
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

                stock.CurrentQuantity += item.Quantity;
                stock.UpdatedAt = now;

                _context.InventoryMovementLedgers.Add(new InventoryMovementLedger
                {
                    DocumentType = documentType,
                    DocumentId = documentId,
                    WarehouseId = warehouseId,
                    ProductId = item.ProductId,
                    QuantityIn = item.Quantity,
                    QuantityOut = 0,
                    BalanceAfter = stock.CurrentQuantity,
                    CreatedByUserId = CurrentUserId,
                    CreatedAt = now,
                    Notes = note
                });
            }
        }

        private async Task<(bool Success, string? Message)> ApplyReceiptToStockWithRetryAsync(int warehouseId, List<WarehouseReceiptItemVM> items, string documentType, string documentId, string? note, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await ApplyReceiptToStockAsync(warehouseId, items, documentType, documentId, note, cancellationToken);
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
                        return (false, "تداخل همزمان در بروزرسانی موجودی رخ داد. لطفاً دوباره تلاش کنید.");
                    }
                }
            }

            return (false, "بروزرسانی موجودی ناموفق بود.");
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

        private async Task<(bool Success, string? Message)> ApplyCountingApprovalToStockWithRetryAsync(int warehouseId, List<InventoryCountingItemVM> items, string documentType, string documentId, string? note, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await ApplyCountingApprovalToStockAsync(warehouseId, items, documentType, documentId, note, cancellationToken);
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
                        return (false, "تداخل همزمان در تایید انبارگردانی رخ داد. لطفاً مجدداً تلاش کنید.");
                    }
                }
            }

            return (false, "تایید انبارگردانی ناموفق بود.");
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

        private async Task PopulateVendorOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
        {
            options.Clear();
            var items = await _context.Vendors
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

        private async Task<List<SelectListItem>> BuildVendorOptionsAsync(int? selectedId, CancellationToken cancellationToken)
        {
            var options = new List<SelectListItem>();
            await PopulateVendorOptionsAsync(options, cancellationToken);
            options.Insert(0, new SelectListItem { Value = string.Empty, Text = "همه تامین‌کنندگان", Selected = !selectedId.HasValue });
            if (selectedId.HasValue)
            {
                foreach (var option in options)
                {
                    option.Selected = option.Value == selectedId.Value.ToString();
                }
            }

            return options;
        }

        private async Task PopulateEmployerOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
        {
            options.Clear();
            var items = await _context.Employers
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

        private async Task PopulateManagerOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
        {
            options.Clear();
            var items = await _context.Users
                .AsNoTracking()
                .OrderBy(item => item.FullName)
                .Select(item => new SelectListItem
                {
                    Value = item.Id,
                    Text = item.FullName ?? item.UserName ?? "کاربر"
                })
                .ToListAsync(cancellationToken);

            options.AddRange(items);
        }

        private async Task<List<SelectListItem>> BuildManagerOptionsAsync(int? selectedId, CancellationToken cancellationToken)
        {
            var options = new List<SelectListItem>();
            await PopulateManagerOptionsAsync(options, cancellationToken);
            options.Insert(0, new SelectListItem { Value = string.Empty, Text = "همه کاربران", Selected = !selectedId.HasValue });
            if (selectedId.HasValue)
            {
                foreach (var option in options)
                {
                    option.Selected = option.Value == selectedId.Value.ToString();
                }
            }

            return options;
        }

        private async Task<List<SelectListItem>> BuildProductOptionsAsync(int? selectedId, CancellationToken cancellationToken)
        {
            var options = new List<SelectListItem>();
            var items = await _context.Products
                .AsNoTracking()
                .Where(item => !item.IsDeleted)
                .OrderBy(item => item.Name)
                .Select(item => new SelectListItem
                {
                    Value = item.Id.ToString(),
                    Text = $"{item.Name} ({item.Code})",
                    Selected = selectedId.HasValue && selectedId.Value == item.Id
                })
                .ToListAsync(cancellationToken);

            options.Add(new SelectListItem { Value = string.Empty, Text = "همه کالاها", Selected = !selectedId.HasValue });
            options.AddRange(items);
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

        private async Task<string> BuildNextReceiptNumberAsync(CancellationToken cancellationToken)
        {
            var count = await _context.WarehouseReceipts.CountAsync(cancellationToken);
            return $"WR-{count + 1:00000}";
        }

        private async Task<string> BuildNextIssuanceNumberAsync(CancellationToken cancellationToken)
        {
            var count = await _context.WarehouseIssuances.CountAsync(cancellationToken);
            return $"WI-{count + 1:00000}";
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

        private async Task<string> ResolveVendorNameAsync(int? vendorId, string? fallbackName, CancellationToken cancellationToken)
        {
            if (vendorId.HasValue)
            {
                var vendorName = await _context.Vendors
                    .AsNoTracking()
                    .Where(item => item.Id == vendorId.Value)
                    .Select(item => item.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(vendorName))
                {
                    return vendorName;
                }
            }

            return string.IsNullOrWhiteSpace(fallbackName) ? "نامشخص" : fallbackName.Trim();
        }

        private async Task<string> ResolveEmployerNameAsync(int? employerId, string? fallbackName, CancellationToken cancellationToken)
        {
            if (employerId.HasValue)
            {
                var employerName = await _context.Employers
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
                : await _context.Users
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

            var userNames = await _context.Users
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
