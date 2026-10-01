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
public class WarehouseTransferController : Controller
    {
        private readonly InventoryDbContext _context;
        private readonly IdentityDbContext _identityContext;
        private readonly IAuthorizationFacade _authorizationFacade;
        private readonly WarehouseDashboardService _warehouseDashboardService;
        private readonly WorkflowService _workflowService;
        private readonly WorkflowDetailService _workflowDetailService;

        public WarehouseTransferController(
            InventoryDbContext context,
            IdentityDbContext identityContext,
            IAuthorizationFacade authorizationFacade,
            WarehouseDashboardService warehouseDashboardService,
            WorkflowService workflowService,
            WorkflowDetailService workflowDetailService)
        {
            _context = context;
            _identityContext = identityContext;
            _authorizationFacade = authorizationFacade;
            _warehouseDashboardService = warehouseDashboardService;
            _workflowService = workflowService;
            _workflowDetailService = workflowDetailService;
        }

        private string? CurrentUserId => User?.Identity?.IsAuthenticated == true ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null;

        [HttpGet]
        public async Task<IActionResult> TransferRequests(string? searchTerm, string? status, int? sourceWarehouseId, int? destinationWarehouseId, int? productId, int? requestedById, string? dateFrom, string? dateTo, CancellationToken cancellationToken)
        {
            var items = await _warehouseDashboardService.GetTransferRequestsAsync(
                searchTerm,
                status,
                sourceWarehouseId,
                destinationWarehouseId,
                productId,
                requestedById,
                dateFrom,
                dateTo,
                cancellationToken);

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
            return View("~/Views/Warehouse/TransferRequests.cshtml", items);
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
            ViewBag.WorkflowDetail = await _workflowDetailService.BuildAsync(
                nameof(InventoryTransferRequest),
                request.Id,
                $"درخواست انتقال #{request.Id}",
                request.Description ?? $"{request.Product?.Name} / {request.Quantity:N3}",
                currentUserId ?? string.Empty,
                "Warehouse.Approve",
                cancellationToken);

            return View("~/Views/Warehouse/TransferRequestDetails.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTransferRequest(CancellationToken cancellationToken)
        {
            var model = new InventoryTransferRequestCreateVM();
            await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
            await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
            return View("~/Views/Warehouse/CreateTransferRequest.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTransferRequest(InventoryTransferRequestCreateVM model, CancellationToken cancellationToken)
        {
            if (model.SourceWarehouseId == model.DestinationWarehouseId)
            {
                ModelState.AddModelError(nameof(model.DestinationWarehouseId), "انبار مبدا و مقصد نمی‌تواند یکسان باشد.");
            }

            var product = await _context.Products.FirstOrDefaultAsync(item => item.Id == model.ProductId && !item.IsDeleted && item.IsActive, cancellationToken);
            if (product == null)
            {
                ModelState.AddModelError(nameof(model.ProductId), "کالای انتخاب‌شده معتبر نیست.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulateWarehouseOptionsAsync(model.WarehouseOptions, cancellationToken);
                return View("~/Views/Warehouse/CreateTransferRequest.cshtml", model);
            }

            var request = new InventoryTransferRequest
            {
                SourceWarehouseId = model.SourceWarehouseId,
                DestinationWarehouseId = model.DestinationWarehouseId,
                ProductId = model.ProductId,
                Quantity = model.Quantity,
                RequestedByUserId = CurrentUserId ?? string.Empty,
                Description = model.Description?.Trim(),
                Status = WorkflowStatus.PendingApproval,
                CreatedAt = DateTime.Now
            };

            _context.InventoryTransferRequests.Add(request);
            await _context.SaveChangesAsync(cancellationToken);

            await _workflowService.StartDirectAssignmentAsync(
                nameof(InventoryTransferRequest),
                request.Id,
                request.RequestedByUserId,
                null,
                DateTimeOffset.UtcNow.AddDays(7),
                cancellationToken);

            await WriteAuditLogAsync(
                "Create",
                "InventoryTransferRequest",
                request.Id.ToString(),
                null,
                new
                {
                    request.SourceWarehouseId,
                    request.DestinationWarehouseId,
                    request.ProductId,
                    request.Quantity,
                    request.Status
                },
                cancellationToken);

            return RedirectToAction(nameof(TransferRequestDetails), new { id = request.Id });
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

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
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
            await _workflowService.RecordDecisionAsync(
                nameof(InventoryTransferRequest),
                request.Id,
                1,
                currentUserId,
                WorkflowStatus.Approved,
                cancellationToken: cancellationToken);

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
                    request.ApprovedAt
                },
                cancellationToken);

            TempData["WarehouseMessage"] = "درخواست انتقال با موفقیت انجام شد.";
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
            await _workflowService.RecordDecisionAsync(
                nameof(InventoryTransferRequest),
                request.Id,
                1,
                currentUserId,
                WorkflowStatus.Rejected,
                request.RejectReason,
                cancellationToken);

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

        private async Task<List<SelectListItem>> BuildWarehouseOptionsAsync(int? selectedId, CancellationToken cancellationToken)
        {
            var options = new List<SelectListItem>();
            await PopulateWarehouseOptionsAsync(options, cancellationToken);
            options.Insert(0, new SelectListItem { Value = string.Empty, Text = "همه انبارها", Selected = !selectedId.HasValue });
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
