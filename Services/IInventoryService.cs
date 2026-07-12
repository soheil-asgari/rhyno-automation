using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services
{
    public sealed class WarehouseReceiptDto
    {
        public int Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public List<WarehouseReceiptItemDto> Items { get; set; } = new();
    }

    public sealed class WarehouseReceiptItemDto
    {
        public int ProductId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public sealed class InventoryStockDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
    }

    public sealed class ProductOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public sealed class WarehouseReceiptOptionDto
    {
        public int Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public string SupplierOrSource { get; set; } = string.Empty;
        public string DateShamsi { get; set; } = string.Empty;
    }

    public interface IInventoryService
    {
        Task<WarehouseReceiptDto?> GetReceiptDetailsAsync(int receiptId, CancellationToken cancellationToken);
        Task<List<InventoryStockDto>> GetInventoryStocksAsync(int? warehouseId, CancellationToken cancellationToken);
        Task<WarehouseReceipt?> GetWarehouseReceiptForPrintAsync(int id, CancellationToken cancellationToken);
        Task<List<int>> GetActiveProductIdsAsync(List<int> productIds, CancellationToken cancellationToken);
        Task<bool> WarehouseReceiptExistsAsync(int receiptId, CancellationToken cancellationToken);
        Task<List<ProductOptionDto>> GetActiveProductOptionsAsync(CancellationToken cancellationToken);
        Task<List<WarehouseReceiptOptionDto>> GetAvailableWarehouseReceiptOptionsAsync(List<int> excludedReceiptIds, int? currentReceiptId, CancellationToken cancellationToken);

        /// <summary>
        /// Checks whether an employer has any warehouse issuances referencing them.
        /// Used by EmployersController to decide whether to hard-delete or soft-delete.
        /// Exposed here so the controller does not need to inject InventoryDbContext directly,
        /// preserving module boundary isolation (Bمب سوم fix).
        /// </summary>
        Task<bool> EmployerHasIssuancesAsync(int employerId, CancellationToken cancellationToken);
    }
}
