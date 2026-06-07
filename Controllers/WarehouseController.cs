using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using OfficeAutomation.Data;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [RequireAccessArea("Warehouse")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int DefaultWarehouseId = 1;

        public WarehouseController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? CurrentUserId => User?.Identity?.IsAuthenticated == true ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var model = new WarehouseDashboardVM
            {
                ProductCount = await _context.Products.CountAsync(item => !item.IsDeleted, cancellationToken),
                WarehouseCount = await _context.Warehouses.CountAsync(item => item.IsActive, cancellationToken),
                ReceiptCount = await _context.WarehouseReceipts.CountAsync(cancellationToken),
                IssuanceCount = await _context.WarehouseIssuances.CountAsync(cancellationToken),
                CountingDraftCount = await _context.InventoryCountings.CountAsync(item => item.Status == "Draft", cancellationToken),
                LowStockCount = await _context.InventoryStocks.CountAsync(item => item.CurrentQuantity <= item.Product.MinimumStock, cancellationToken)
            };

            return View(model);
        }

        [HttpGet]
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
        public IActionResult CreateProduct()
        {
            return View(new ProductUpsertVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                MinimumStock = entity.MinimumStock,
                IsActive = entity.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                entity.MinimumStock,
                entity.IsActive
            };

            entity.Code = model.Code.Trim();
            entity.Name = model.Name.Trim();
            entity.Unit = model.Unit.Trim();
            entity.Description = model.Description?.Trim();
            entity.MinimumStock = model.MinimumStock;
            entity.IsActive = model.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                TempData["WarehouseMessage"] = "کالا به دلیل وجود گردش، حذف نرم شد.";
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
            TempData["WarehouseMessage"] = "کالا با موفقیت حذف شد.";
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                ManagerUserId = string.IsNullOrWhiteSpace(model.ManagerUserId) ? null : model.ManagerUserId,
                IsActive = model.IsActive,
                IsClosed = model.IsClosed,
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
                ManagerUserId = entity.ManagerUserId,
                IsActive = entity.IsActive,
                IsClosed = entity.IsClosed
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
                entity.ManagerUserId,
                entity.IsActive,
                entity.IsClosed
            };

            entity.Code = model.Code.Trim();
            entity.Name = model.Name.Trim();
            entity.Location = model.Location?.Trim();
            entity.ManagerUserId = string.IsNullOrWhiteSpace(model.ManagerUserId) ? null : model.ManagerUserId;
            entity.IsActive = model.IsActive;
            entity.IsClosed = model.IsClosed;

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
        public async Task<IActionResult> Receipts(string? searchTerm, int? warehouseId, CancellationToken cancellationToken)
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

            var items = await query
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);

            ViewBag.SearchTerm = searchTerm;
            ViewBag.WarehouseId = warehouseId;
            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(warehouseId, cancellationToken);
            return View(items);
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

            var receiptApplied = await ApplyReceiptToStockWithRetryAsync(model.WarehouseId, validItems, cancellationToken);
            if (!receiptApplied.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                ModelState.AddModelError(string.Empty, receiptApplied.Message ?? "بروزرسانی موجودی با خطا مواجه شد.");
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
                await PopulateVendorOptionsAsync(model.VendorOptions, cancellationToken);
                return View(model);
            }

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
                    ItemCount = entity.Items.Count
                },
                cancellationToken);

            return RedirectToAction(nameof(Receipts));
        }

        [HttpGet]
        public async Task<IActionResult> Issuances(string? searchTerm, int? warehouseId, CancellationToken cancellationToken)
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

            var items = await query
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);

            ViewBag.SearchTerm = searchTerm;
            ViewBag.WarehouseId = warehouseId;
            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(warehouseId, cancellationToken);
            return View(items);
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

            var deductionResult = await ApplyIssuanceToStockWithRetryAsync(model.WarehouseId, validItems, cancellationToken);
            if (!deductionResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                ModelState.AddModelError(string.Empty, deductionResult.Message ?? "موجودی کافی نیست.");
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
                await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
                return View(model);
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
                    ItemCount = entity.Items.Count
                },
                cancellationToken);

            return RedirectToAction(nameof(Issuances));
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
                    UpdatedAt = item.UpdatedAt
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
                    Quantity = group.Sum(item => item.Quantity)
                })
                .ToDictionaryAsync(item => $"{item.WarehouseId}:{item.ProductId}", item => item.Quantity, cancellationToken);

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
                row.TotalInput = receiptSums.TryGetValue(key, out var inputQty) ? inputQty : 0;
                row.TotalOutput = issuanceSums.TryGetValue(key, out var outputQty) ? outputQty : 0;
            }

            filter.Items = baseRows;

            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(filter.WarehouseId, cancellationToken);
            return View(filter);
        }

        [HttpGet]
        public async Task<IActionResult> Countings(string? searchTerm, int? warehouseId, CancellationToken cancellationToken)
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

            ViewBag.SearchTerm = searchTerm;
            ViewBag.WarehouseId = warehouseId;
            ViewBag.WarehouseOptions = await BuildWarehouseOptionsAsync(warehouseId, cancellationToken);
            return View(items);
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
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TransferRequests(CancellationToken cancellationToken)
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

            var currentUserId = CurrentUserId;
            ViewBag.IsManagerApproval = !string.IsNullOrWhiteSpace(currentUserId) &&
                                        await _context.Warehouses
                                            .AsNoTracking()
                                            .AnyAsync(item => item.IsActive && item.ManagerUserId == currentUserId, cancellationToken);
            ViewBag.CurrentUserId = currentUserId;
            return View(items);
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
                ModelState.AddModelError(nameof(model.DestinationWarehouseId), "مبدا و مقصد نمی‌تواند یکسان باشد.");
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
                Status = "PendingManager",
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
                    entity.Status
                },
                cancellationToken);

            TempData["WarehouseMessage"] = "درخواست انتقال ثبت شد و در انتظار تایید مدیر مبدا قرار گرفت.";
            return RedirectToAction(nameof(TransferRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

            if (request.Status != "PendingManager")
            {
                TempData["WarehouseMessage"] = "این درخواست قبلاً تعیین تکلیف شده است.";
                return RedirectToAction(nameof(TransferRequests));
            }

            var isSourceManager = await _context.Warehouses
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.SourceWarehouseId && item.ManagerUserId == currentUserId, cancellationToken);
            if (!isSourceManager && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var issueResult = await ApplyIssuanceToStockWithRetryAsync(
                request.SourceWarehouseId,
                new List<WarehouseIssuanceItemVM> { new() { ProductId = request.ProductId, Quantity = request.Quantity } },
                cancellationToken);

            if (!issueResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["WarehouseMessage"] = issueResult.Message ?? "موجودی مبدا کافی نیست.";
                return RedirectToAction(nameof(TransferRequests));
            }

            var receiptResult = await ApplyReceiptToStockWithRetryAsync(
                request.DestinationWarehouseId,
                new List<WarehouseReceiptItemVM> { new() { ProductId = request.ProductId, Quantity = request.Quantity, UnitPrice = 0 } },
                cancellationToken);

            if (!receiptResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["WarehouseMessage"] = receiptResult.Message ?? "ثبت موجودی مقصد با خطا مواجه شد.";
                return RedirectToAction(nameof(TransferRequests));
            }

            request.Status = "Approved";
            request.ApprovedByUserId = currentUserId;
            request.ApprovedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Approve",
                "InventoryTransferRequest",
                request.Id.ToString(),
                new { Status = "PendingManager" },
                new
                {
                    request.Status,
                    request.ApprovedByUserId,
                    request.ApprovedAt
                },
                cancellationToken);

            TempData["WarehouseMessage"] = "درخواست انتقال تایید شد و موجودی دو انبار به‌روزرسانی شد.";
            return RedirectToAction(nameof(TransferRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTransferRequest(int id, CancellationToken cancellationToken)
        {
            var currentUserId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Forbid();
            }

            var request = await _context.InventoryTransferRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (request == null)
            {
                return NotFound();
            }

            if (request.Status != "PendingManager")
            {
                TempData["WarehouseMessage"] = "این درخواست قبلاً تعیین تکلیف شده است.";
                return RedirectToAction(nameof(TransferRequests));
            }

            var isSourceManager = await _context.Warehouses
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.SourceWarehouseId && item.ManagerUserId == currentUserId, cancellationToken);
            if (!isSourceManager && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            request.Status = "Rejected";
            request.ApprovedByUserId = currentUserId;
            request.ApprovedAt = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            await WriteAuditLogAsync(
                "Reject",
                "InventoryTransferRequest",
                request.Id.ToString(),
                new { Status = "PendingManager" },
                new
                {
                    request.Status,
                    request.ApprovedByUserId,
                    request.ApprovedAt
                },
                cancellationToken);

            TempData["WarehouseMessage"] = "درخواست انتقال رد شد.";
            return RedirectToAction(nameof(TransferRequests));
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
                var approvalResult = await ApplyCountingApprovalToStockWithRetryAsync(model.WarehouseId, validItems, cancellationToken);
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

            return RedirectToAction(nameof(Countings));
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

            var approvalResult = await ApplyCountingApprovalToStockWithRetryAsync(counting.WarehouseId, itemVMs, cancellationToken);
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

            return RedirectToAction(nameof(Countings));
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
            await ValidateProductsExistenceAsync(model.Items.Select(item => item.ProductId), cancellationToken);
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

        private async Task ApplyReceiptToStockAsync(int warehouseId, List<WarehouseReceiptItemVM> items, CancellationToken cancellationToken)
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
            }
        }

        private async Task<(bool Success, string? Message)> ApplyReceiptToStockWithRetryAsync(int warehouseId, List<WarehouseReceiptItemVM> items, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await ApplyReceiptToStockAsync(warehouseId, items, cancellationToken);
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

        private async Task<(bool Success, string? Message)> ApplyIssuanceToStockAsync(int warehouseId, List<WarehouseIssuanceItemVM> items, CancellationToken cancellationToken)
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
            }

            return (true, null);
        }

        private async Task<(bool Success, string? Message)> ApplyIssuanceToStockWithRetryAsync(int warehouseId, List<WarehouseIssuanceItemVM> items, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var result = await ApplyIssuanceToStockAsync(warehouseId, items, cancellationToken);
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

        private async Task ApplyCountingApprovalToStockAsync(int warehouseId, List<InventoryCountingItemVM> items, CancellationToken cancellationToken)
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
            }
        }

        private async Task<(bool Success, string? Message)> ApplyCountingApprovalToStockWithRetryAsync(int warehouseId, List<InventoryCountingItemVM> items, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await ApplyCountingApprovalToStockAsync(warehouseId, items, cancellationToken);
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
            return Task.CompletedTask;
        }
    }
}
