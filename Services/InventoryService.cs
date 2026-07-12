using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;

namespace OfficeAutomation.Services
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly InventoryDbContext _context;

        public InventoryService(InventoryDbContext context)
        {
            _context = context;
        }

        public async Task<WarehouseReceiptDto?> GetReceiptDetailsAsync(int receiptId, CancellationToken cancellationToken)
        {
            var receipt = await _context.WarehouseReceipts
                .AsNoTracking()
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .FirstOrDefaultAsync(item => item.Id == receiptId, cancellationToken);

            if (receipt == null)
            {
                return null;
            }

            return new WarehouseReceiptDto
            {
                Id = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                Items = receipt.Items
                    .Where(item => item.ProductId > 0 && item.Quantity > 0)
                    .Select(item => new WarehouseReceiptItemDto
                    {
                        ProductId = item.ProductId,
                        ItemName = item.Product?.Name ?? $"کالا {item.ProductId}",
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    })
                    .ToList()
            };
        }

        public async Task<List<InventoryStockDto>> GetInventoryStocksAsync(int? warehouseId, CancellationToken cancellationToken)
        {
            var query = _context.InventoryStocks
                .AsNoTracking()
                .Include(item => item.Product)
                .Include(item => item.Warehouse)
                .AsQueryable();

            if (warehouseId.HasValue)
            {
                query = query.Where(item => item.WarehouseId == warehouseId.Value);
            }

            var items = await query.OrderBy(item => item.Product.Name).ToListAsync(cancellationToken);

            return items.Select(item => new InventoryStockDto
            {
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? string.Empty,
                ProductCode = item.Product?.Code ?? string.Empty,
                WarehouseName = item.Warehouse?.Name ?? string.Empty,
                Unit = item.Product?.Unit ?? string.Empty,
                CurrentQuantity = item.CurrentQuantity
            }).ToList();
        }

        public async Task<WarehouseReceipt?> GetWarehouseReceiptForPrintAsync(int id, CancellationToken cancellationToken)
        {
            return await _context.WarehouseReceipts
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        }

        public async Task<List<int>> GetActiveProductIdsAsync(List<int> productIds, CancellationToken cancellationToken)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted && productIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> WarehouseReceiptExistsAsync(int receiptId, CancellationToken cancellationToken)
        {
            return await _context.WarehouseReceipts
                .AsNoTracking()
                .AnyAsync(item => item.Id == receiptId, cancellationToken);
        }

        public async Task<List<ProductOptionDto>> GetActiveProductOptionsAsync(CancellationToken cancellationToken)
        {
            var items = await _context.Products
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.Name)
                .Select(item => new ProductOptionDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Code = item.Code
                })
                .ToListAsync(cancellationToken);

            return items;
        }

        public async Task<List<WarehouseReceiptOptionDto>> GetAvailableWarehouseReceiptOptionsAsync(List<int> excludedReceiptIds, int? currentReceiptId, CancellationToken cancellationToken)
        {
            var receiptItems = await _context.WarehouseReceipts
                .AsNoTracking()
                .Where(item =>
                    !excludedReceiptIds.Contains(item.Id) ||
                    (currentReceiptId.HasValue && item.Id == currentReceiptId.Value))
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new WarehouseReceiptOptionDto
                {
                    Id = item.Id,
                    ReceiptNumber = item.ReceiptNumber,
                    SupplierOrSource = item.SupplierOrSource,
                    DateShamsi = item.DateShamsi
                })
                .Take(300)
                .ToListAsync(cancellationToken);

            return receiptItems;
        }

        public async Task<bool> EmployerHasIssuancesAsync(int employerId, CancellationToken cancellationToken)
        {
            return await _context.WarehouseIssuances
                .AsNoTracking()
                .AnyAsync(item => item.EmployerId == employerId, cancellationToken);
        }
    }
}
