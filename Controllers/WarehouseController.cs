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
    public class WarehouseController : Controller
    {
        private readonly InventoryDbContext _context;
        private readonly IdentityDbContext _identityContext;
        private readonly IAuthorizationFacade _authorizationFacade;
        private readonly WarehouseDashboardService _warehouseDashboardService;

        public WarehouseController(
            InventoryDbContext context,
            IdentityDbContext identityContext,
            IAuthorizationFacade authorizationFacade,
            WarehouseDashboardService warehouseDashboardService)
        {
            _context = context;
            _identityContext = identityContext;
            _authorizationFacade = authorizationFacade;
            _warehouseDashboardService = warehouseDashboardService;
        }

        private string? CurrentUserId => User?.Identity?.IsAuthenticated == true ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var model = await _warehouseDashboardService.BuildDashboardAsync(cancellationToken);
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

        private async Task PopulateManagerOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
        {
            options.Clear();
            var items = await _identityContext.Users
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
