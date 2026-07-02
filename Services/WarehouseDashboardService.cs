using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services;

public sealed class WarehouseDashboardService
{
    private readonly InventoryDbContext _context;
    private readonly IdentityDbContext _identityContext;
    private readonly NotificationService _notificationService;

    public WarehouseDashboardService(InventoryDbContext context, IdentityDbContext identityContext, NotificationService notificationService)
    {
        _context = context;
        _identityContext = identityContext;
        _notificationService = notificationService;
    }

    public async Task<WarehouseDashboardVM> BuildDashboardAsync(CancellationToken cancellationToken = default)
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

        await PublishLowStockNotificationsAsync(criticalStocks, cancellationToken);

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

        return new WarehouseDashboardVM
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
    }

    private async Task PublishLowStockNotificationsAsync(
        IReadOnlyCollection<WarehouseLowStockItemVM> criticalStocks,
        CancellationToken cancellationToken)
    {
        if (criticalStocks.Count == 0)
        {
            return;
        }

        var recipientIds = await GetWarehouseNotificationRecipientsAsync(cancellationToken);
        if (recipientIds.Count == 0)
        {
            return;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        foreach (var stock in criticalStocks.Take(8))
        {
            var severity = stock.CurrentQuantity < 0 ? NotificationSeverity.Danger : NotificationSeverity.Warning;
            var title = stock.CurrentQuantity < 0 ? "موجودی منفی انبار" : "هشدار حداقل موجودی";
            var message = $"{stock.ProductName} در {stock.WarehouseName}: موجودی {stock.CurrentQuantity:N0}، حداقل {stock.MinimumStock:N0}";
            var sourceEntityId = unchecked((stock.WarehouseId * 397) ^ stock.ProductId);

            foreach (var recipientId in recipientIds)
            {
                await _notificationService.UpsertActiveAsync(
                    recipientId,
                    title,
                    message,
                    severity,
                    "/Warehouse/Stock",
                    "Warehouse",
                    "InventoryStock",
                    sourceEntityId,
                    expiresAt,
                    cancellationToken);
            }
        }
    }

    private async Task<List<string>> GetWarehouseNotificationRecipientsAsync(CancellationToken cancellationToken)
    {
        var roleBasedUsers =
            from userRole in _identityContext.UserRoles.AsNoTracking()
            join rolePermission in _identityContext.RolePermissions.AsNoTracking()
                on userRole.RoleId equals rolePermission.RoleId
            where rolePermission.PermissionKey == "Warehouse.View" || rolePermission.PermissionKey == "Warehouse.Approve"
            select userRole.UserId;

        var flagBasedUsers = _identityContext.Users
            .AsNoTracking()
            .Where(user => user.CanAccessWarehouse)
            .Select(user => user.Id);

        return await roleBasedUsers
            .Union(flagBasedUsers)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<InventoryTransferRequest>> GetTransferRequestsAsync(
        string? searchTerm,
        string? status,
        int? sourceWarehouseId,
        int? destinationWarehouseId,
        int? productId,
        int? requestedById,
        string? dateFrom,
        string? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryTransferRequests
            .AsNoTracking()
            .Include(item => item.SourceWarehouse)
            .Include(item => item.DestinationWarehouse)
            .Include(item => item.Product)
            .Include(item => item.RequestedByUser)
            .Include(item => item.ApprovedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(item =>
                (item.Product != null && (item.Product.Name.Contains(term) || item.Product.Code.Contains(term))) ||
                (item.Description != null && item.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = WorkflowStatus.Normalize(status);
            query = query.Where(item => item.Status == normalizedStatus || item.Status == status.Trim());
        }

        if (sourceWarehouseId.HasValue)
        {
            query = query.Where(item => item.SourceWarehouseId == sourceWarehouseId.Value);
        }

        if (destinationWarehouseId.HasValue)
        {
            query = query.Where(item => item.DestinationWarehouseId == destinationWarehouseId.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(item => item.ProductId == productId.Value);
        }

        if (requestedById.HasValue)
        {
            var requestedByUserId = requestedById.Value.ToString();
            query = query.Where(item => item.RequestedByUserId == requestedByUserId);
        }

        if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var fromDate))
        {
            query = query.Where(item => item.CreatedAt >= fromDate.Date);
        }

        if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var toDate))
        {
            var inclusiveToDate = toDate.Date.AddDays(1).AddTicks(-1);
            query = query.Where(item => item.CreatedAt <= inclusiveToDate);
        }

        return await query
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

