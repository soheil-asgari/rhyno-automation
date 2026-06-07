using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Auditing;

namespace OfficeAutomation.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, ApplicationRole, string>
    {
        private readonly IAuditContextProvider? _auditContextProvider;
        private bool _isWritingAuditLog;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IAuditContextProvider? auditContextProvider = null)
            : base(options)
        {
            _auditContextProvider = auditContextProvider;
        }

        public DbSet<InsuranceList> InsuranceLists { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Waybill> Waybills { get; set; }
        public DbSet<InsuranceEmployee> InsuranceEmployees { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<HumanCapitalEmployee> HumanCapitalEmployees { get; set; }
        public DbSet<HumanCapitalSalaryHistory> HumanCapitalSalaryHistories { get; set; }
        public DbSet<HumanCapitalStatusHistory> HumanCapitalStatusHistories { get; set; }
        public DbSet<Letter> Letters { get; set; }
        public DbSet<Leave> Leaves { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<PayrollList> PayrollLists { get; set; }
        public DbSet<PayrollItem> PayrollItems { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<WarehouseReceipt> WarehouseReceipts { get; set; }
        public DbSet<WarehouseReceiptItem> WarehouseReceiptItems { get; set; }
        public DbSet<WarehouseIssuance> WarehouseIssuances { get; set; }
        public DbSet<WarehouseIssuanceItem> WarehouseIssuanceItems { get; set; }
        public DbSet<InventoryStock> InventoryStocks { get; set; }
        public DbSet<InventoryCounting> InventoryCountings { get; set; }
        public DbSet<InventoryCountingItem> InventoryCountingItems { get; set; }
        public DbSet<WarehouseClosing> WarehouseClosings { get; set; }
        public DbSet<WarehouseClosingItem> WarehouseClosingItems { get; set; }
        public DbSet<InventoryOpeningBalanceLedger> InventoryOpeningBalanceLedgers { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Employer> Employers { get; set; }
        public DbSet<InventoryTransferRequest> InventoryTransferRequests { get; set; }
        public DbSet<WorkflowRoute> WorkflowRoutes { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_isWritingAuditLog)
            {
                return await base.SaveChangesAsync(cancellationToken);
            }

            ChangeTracker.DetectChanges();

            var auditEntries = PrepareAuditEntries();
            var result = await base.SaveChangesAsync(cancellationToken);

            if (auditEntries.Count == 0)
            {
                return result;
            }

            foreach (var auditEntry in auditEntries)
            {
                auditEntry.FinalizeTemporaryProperties();
                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            _isWritingAuditLog = true;
            try
            {
                await base.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _isWritingAuditLog = false;
            }

            return result;
        }

        private List<PendingAuditLogEntry> PrepareAuditEntries()
        {
            var requestInfo = _auditContextProvider?.GetCurrent() ?? new AuditRequestInfo(null, null, null);
            var auditEntries = new List<PendingAuditLogEntry>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State is EntityState.Detached or EntityState.Unchanged)
                {
                    continue;
                }

                if (entry.Entity is AuditLog)
                {
                    continue;
                }

                var auditEntry = new PendingAuditLogEntry(entry, requestInfo);

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.Action = "Create";
                        PopulateAddedEntry(auditEntry);
                        break;
                    case EntityState.Modified:
                        auditEntry.Action = "Update";
                        PopulateModifiedEntry(auditEntry);
                        break;
                    case EntityState.Deleted:
                        auditEntry.Action = "Delete";
                        PopulateDeletedEntry(auditEntry);
                        break;
                }

                if (auditEntry.AffectedColumns.Count == 0 &&
                    auditEntry.OldValues.Count == 0 &&
                    auditEntry.NewValues.Count == 0)
                {
                    continue;
                }

                auditEntries.Add(auditEntry);
            }

            return auditEntries;
        }

        private static void PopulateAddedEntry(PendingAuditLogEntry auditEntry)
        {
            foreach (var property in auditEntry.Entry.Properties)
            {
                if (property.IsTemporary)
                {
                    auditEntry.TemporaryProperties.Add(property);
                    continue;
                }

                auditEntry.AffectedColumns.Add(property.Metadata.Name);
                auditEntry.NewValues[property.Metadata.Name] = PendingAuditLogEntry.NormalizeValue(property.CurrentValue);
            }
        }

        private static void PopulateModifiedEntry(PendingAuditLogEntry auditEntry)
        {
            foreach (var property in auditEntry.Entry.Properties)
            {
                if (property.IsTemporary)
                {
                    auditEntry.TemporaryProperties.Add(property);
                    continue;
                }

                if (property.Metadata.IsPrimaryKey() || !property.IsModified || Equals(property.OriginalValue, property.CurrentValue))
                {
                    continue;
                }

                auditEntry.AffectedColumns.Add(property.Metadata.Name);
                auditEntry.OldValues[property.Metadata.Name] = PendingAuditLogEntry.NormalizeValue(property.OriginalValue);
                auditEntry.NewValues[property.Metadata.Name] = PendingAuditLogEntry.NormalizeValue(property.CurrentValue);
            }
        }

        private static void PopulateDeletedEntry(PendingAuditLogEntry auditEntry)
        {
            foreach (var property in auditEntry.Entry.Properties)
            {
                auditEntry.AffectedColumns.Add(property.Metadata.Name);
                auditEntry.OldValues[property.Metadata.Name] = PendingAuditLogEntry.NormalizeValue(property.OriginalValue);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Letter>()
                .HasOne(l => l.Sender)
                .WithMany()
                .HasForeignKey(l => l.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Letter>()
                .HasOne(l => l.Receiver)
                .WithMany()
                .HasForeignKey(l => l.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Letter>()
                .HasOne(l => l.FinalReceiver)
                .WithMany()
                .HasForeignKey(l => l.FinalReceiverId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Department>()
                .HasOne(d => d.Manager)
                .WithMany()
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Department>()
                .HasOne(d => d.ManagerEmployee)
                .WithMany()
                .HasForeignKey(d => d.ManagerEmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Employee)
                .WithMany()
                .HasForeignKey(u => u.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .HasOne(u => u.ParentManagerUser)
                .WithMany()
                .HasForeignKey(u => u.ParentManagerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Department>().HasData(
                new Department { Id = 1, Name = "Financial" },
                new Department { Id = 2, Name = "Administrative" },
                new Department { Id = 3, Name = "Technical" },
                new Department { Id = 4, Name = "HR" },
                new Department { Id = 5, Name = "Management" }
            );

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => new { i.InvoiceNumber, i.InvoiceType })
                .IsUnique()
                .HasDatabaseName("IX_Invoice_Number_Type");

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => new { i.InvoiceType, i.DateShamsi })
                .HasDatabaseName("IX_Invoice_Type_DateShamsi");

            modelBuilder.Entity<Invoice>()
                .Property(i => i.InvoiceType)
                .HasMaxLength(20);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.DateShamsi)
                .HasMaxLength(20);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.PartyName)
                .HasMaxLength(150);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.NationalCodeOrEconomicId)
                .HasMaxLength(30);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.SubTotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.VatAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.GrandTotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.Notes)
                .HasMaxLength(600);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<Invoice>()
                .Property(i => i.DeadlineDateShamsi)
                .HasMaxLength(20);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.WarehouseReceipt)
                .WithMany()
                .HasForeignKey(i => i.WarehouseReceiptId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.FollowUpEmployee)
                .WithMany()
                .HasForeignKey(i => i.FollowUpEmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Employer)
                .WithMany(e => e.Invoices)
                .HasForeignKey(i => i.EmployerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InvoiceItem>()
                .Property(i => i.ItemName)
                .HasMaxLength(150);

            modelBuilder.Entity<InvoiceItem>()
                .Property(i => i.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<InvoiceItem>()
                .Property(i => i.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvoiceItem>()
                .Property(i => i.LineSubTotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvoiceItem>()
                .Property(i => i.LineVatAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvoiceItem>()
                .Property(i => i.LineGrandTotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvoiceItem>()
                .HasOne(i => i.Invoice)
                .WithMany(i => i.Items)
                .HasForeignKey(i => i.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoiceItem>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Waybill>()
                .HasIndex(waybill => waybill.WaybillNumber)
                .IsUnique();

            modelBuilder.Entity<Waybill>()
                .Property(waybill => waybill.Weight)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Waybill>()
                .Property(waybill => waybill.TotalFreightCharges)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Waybill>()
                .Property(waybill => waybill.DriverCommission)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Waybill>()
                .Property(waybill => waybill.NetPayToDriver)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HumanCapitalEmployee>()
                .HasOne(employee => employee.Department)
                .WithMany()
                .HasForeignKey(employee => employee.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<HumanCapitalEmployee>()
                .HasIndex(employee => employee.PersonnelCode)
                .IsUnique();

            modelBuilder.Entity<HumanCapitalEmployee>()
                .HasIndex(employee => employee.NationalCode)
                .IsUnique();

            modelBuilder.Entity<HumanCapitalEmployee>()
                .Property(employee => employee.CurrentSalary)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HumanCapitalSalaryHistory>()
                .HasOne(history => history.Employee)
                .WithMany(employee => employee.SalaryHistories)
                .HasForeignKey(history => history.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HumanCapitalSalaryHistory>()
                .HasIndex(history => new { history.EmployeeId, history.EffectiveDate });

            modelBuilder.Entity<HumanCapitalSalaryHistory>()
                .Property(history => history.PreviousSalary)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HumanCapitalSalaryHistory>()
                .Property(history => history.NewSalary)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HumanCapitalStatusHistory>()
                .HasOne(history => history.Employee)
                .WithMany(employee => employee.StatusHistories)
                .HasForeignKey(history => history.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HumanCapitalStatusHistory>()
                .HasIndex(history => new { history.EmployeeId, history.EffectiveDate });

            modelBuilder.Entity<SystemSetting>()
                .HasIndex(setting => setting.ApplicationTitle);

            modelBuilder.Entity<UserPreference>()
                .HasIndex(preference => preference.UserId)
                .IsUnique();

            modelBuilder.Entity<UserPreference>()
                .HasOne(preference => preference.User)
                .WithMany()
                .HasForeignKey(preference => preference.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PayrollList>()
                .HasIndex(list => new { list.Year, list.Month })
                .IsUnique();

            modelBuilder.Entity<PayrollList>()
                .Property(list => list.Status)
                .HasMaxLength(50);

            modelBuilder.Entity<PayrollList>()
                .Property(list => list.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<PayrollItem>()
                .Property(item => item.EmployeeName)
                .HasMaxLength(120);

            modelBuilder.Entity<PayrollItem>()
                .Property(item => item.BaseSalary)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PayrollItem>()
                .Property(item => item.Allowance)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PayrollItem>()
                .Property(item => item.Overtime)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PayrollItem>()
                .Property(item => item.InsuranceDeduction)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PayrollItem>()
                .Property(item => item.Tax)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PayrollItem>()
                .Property(item => item.NetPayable)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PayrollItem>()
                .HasOne(item => item.PayrollList)
                .WithMany(list => list.Items)
                .HasForeignKey(item => item.PayrollListId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PayrollItem>()
                .HasOne(item => item.HumanCapitalEmployee)
                .WithMany()
                .HasForeignKey(item => item.HumanCapitalEmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InsuranceEmployee>()
                .HasOne(emp => emp.HumanCapitalEmployee)
                .WithMany()
                .HasForeignKey(emp => emp.HumanCapitalEmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Product>()
                .HasIndex(product => product.Code)
                .IsUnique();

            modelBuilder.Entity<Warehouse>()
                .HasIndex(warehouse => warehouse.Code)
                .IsUnique();

            modelBuilder.Entity<Warehouse>()
                .Property(warehouse => warehouse.Code)
                .HasMaxLength(30);

            modelBuilder.Entity<Warehouse>()
                .Property(warehouse => warehouse.Name)
                .HasMaxLength(120);

            modelBuilder.Entity<Warehouse>()
                .Property(warehouse => warehouse.Location)
                .HasMaxLength(200);

            modelBuilder.Entity<Warehouse>()
                .HasOne(warehouse => warehouse.ManagerUser)
                .WithMany()
                .HasForeignKey(warehouse => warehouse.ManagerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Product>()
                .Property(product => product.Code)
                .HasMaxLength(40);

            modelBuilder.Entity<Product>()
                .Property(product => product.Name)
                .HasMaxLength(150);

            modelBuilder.Entity<Product>()
                .Property(product => product.Unit)
                .HasMaxLength(30);

            modelBuilder.Entity<Product>()
                .Property(product => product.Description)
                .HasMaxLength(600);

            modelBuilder.Entity<Product>()
                .Property(product => product.MinimumStock)
                .HasDefaultValue(0);

            modelBuilder.Entity<InventoryTransferRequest>()
                .Property(item => item.Status)
                .HasMaxLength(30);

            modelBuilder.Entity<InventoryTransferRequest>()
                .Property(item => item.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<InventoryTransferRequest>()
                .HasOne(item => item.SourceWarehouse)
                .WithMany()
                .HasForeignKey(item => item.SourceWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryTransferRequest>()
                .HasOne(item => item.DestinationWarehouse)
                .WithMany()
                .HasForeignKey(item => item.DestinationWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryTransferRequest>()
                .HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryTransferRequest>()
                .HasOne(item => item.RequestedByUser)
                .WithMany()
                .HasForeignKey(item => item.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryTransferRequest>()
                .HasOne(item => item.ApprovedByUser)
                .WithMany()
                .HasForeignKey(item => item.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkflowRoute>()
                .Property(item => item.DocumentType)
                .HasMaxLength(60);

            modelBuilder.Entity<WorkflowRoute>()
                .HasIndex(item => new { item.DocumentType, item.StepNumber });

            modelBuilder.Entity<WorkflowRoute>()
                .HasOne(item => item.ApproverUser)
                .WithMany()
                .HasForeignKey(item => item.ApproverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Letter>()
                .Property(item => item.DocumentType)
                .HasMaxLength(60);

            modelBuilder.Entity<RolePermission>()
                .Property(item => item.PermissionKey)
                .HasMaxLength(80);

            modelBuilder.Entity<RolePermission>()
                .Property(item => item.PermissionKey)
                .HasMaxLength(128);

            modelBuilder.Entity<RolePermission>()
                .HasIndex(item => new { item.RoleId, item.PermissionKey })
                .IsUnique();

            modelBuilder.Entity<RolePermission>()
                .HasOne(item => item.Role)
                .WithMany()
                .HasForeignKey(item => item.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WarehouseReceipt>()
                .HasIndex(receipt => receipt.ReceiptNumber)
                .IsUnique();

            modelBuilder.Entity<WarehouseReceipt>()
                .Property(receipt => receipt.ReceiptNumber)
                .HasMaxLength(40);

            modelBuilder.Entity<WarehouseReceipt>()
                .Property(receipt => receipt.DateShamsi)
                .HasMaxLength(20);

            modelBuilder.Entity<WarehouseReceipt>()
                .Property(receipt => receipt.SupplierOrSource)
                .HasMaxLength(200);

            modelBuilder.Entity<WarehouseReceipt>()
                .Property(receipt => receipt.Notes)
                .HasMaxLength(600);

            modelBuilder.Entity<WarehouseReceipt>()
                .HasOne(receipt => receipt.Warehouse)
                .WithMany(warehouse => warehouse.Receipts)
                .HasForeignKey(receipt => receipt.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WarehouseReceipt>()
                .HasOne(receipt => receipt.Vendor)
                .WithMany(vendor => vendor.Receipts)
                .HasForeignKey(receipt => receipt.VendorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WarehouseReceiptItem>()
                .Property(item => item.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<WarehouseReceiptItem>()
                .Property(item => item.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<WarehouseReceiptItem>()
                .HasOne(item => item.WarehouseReceipt)
                .WithMany(receipt => receipt.Items)
                .HasForeignKey(item => item.WarehouseReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WarehouseReceiptItem>()
                .HasOne(item => item.Product)
                .WithMany(product => product.ReceiptItems)
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WarehouseIssuance>()
                .HasIndex(issuance => issuance.IssuanceNumber)
                .IsUnique();

            modelBuilder.Entity<WarehouseIssuance>()
                .Property(issuance => issuance.IssuanceNumber)
                .HasMaxLength(40);

            modelBuilder.Entity<WarehouseIssuance>()
                .Property(issuance => issuance.DateShamsi)
                .HasMaxLength(20);

            modelBuilder.Entity<WarehouseIssuance>()
                .Property(issuance => issuance.DestinationOrDepartment)
                .HasMaxLength(200);

            modelBuilder.Entity<WarehouseIssuance>()
                .Property(issuance => issuance.Notes)
                .HasMaxLength(600);

            modelBuilder.Entity<WarehouseIssuance>()
                .HasOne(issuance => issuance.Warehouse)
                .WithMany(warehouse => warehouse.Issuances)
                .HasForeignKey(issuance => issuance.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WarehouseIssuance>()
                .HasOne(issuance => issuance.Employer)
                .WithMany(employer => employer.Issuances)
                .HasForeignKey(issuance => issuance.EmployerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WarehouseIssuanceItem>()
                .Property(item => item.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<WarehouseIssuanceItem>()
                .HasOne(item => item.WarehouseIssuance)
                .WithMany(issuance => issuance.Items)
                .HasForeignKey(item => item.WarehouseIssuanceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WarehouseIssuanceItem>()
                .HasOne(item => item.Product)
                .WithMany(product => product.IssuanceItems)
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryStock>()
                .HasIndex(stock => new { stock.ProductId, stock.WarehouseId })
                .IsUnique();

            modelBuilder.Entity<InventoryStock>()
                .Property(stock => stock.CurrentQuantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<InventoryStock>()
                .Property(stock => stock.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<InventoryStock>()
                .HasOne(stock => stock.Product)
                .WithMany(product => product.Stocks)
                .HasForeignKey(stock => stock.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryStock>()
                .HasOne(stock => stock.Warehouse)
                .WithMany(warehouse => warehouse.Stocks)
                .HasForeignKey(stock => stock.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryCounting>()
                .HasIndex(counting => counting.DocumentNumber)
                .IsUnique();

            modelBuilder.Entity<InventoryCounting>()
                .Property(counting => counting.DocumentNumber)
                .HasMaxLength(40);

            modelBuilder.Entity<InventoryCounting>()
                .Property(counting => counting.DateShamsi)
                .HasMaxLength(20);

            modelBuilder.Entity<InventoryCounting>()
                .Property(counting => counting.Status)
                .HasMaxLength(20);

            modelBuilder.Entity<InventoryCounting>()
                .Property(counting => counting.Notes)
                .HasMaxLength(600);

            modelBuilder.Entity<InventoryCounting>()
                .HasOne(counting => counting.Warehouse)
                .WithMany(warehouse => warehouse.Countings)
                .HasForeignKey(counting => counting.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryCountingItem>()
                .Property(item => item.SystemQuantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<InventoryCountingItem>()
                .Property(item => item.PhysicalQuantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<InventoryCountingItem>()
                .Property(item => item.DiscrepancyQuantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<InventoryCountingItem>()
                .HasOne(item => item.InventoryCounting)
                .WithMany(counting => counting.Items)
                .HasForeignKey(item => item.InventoryCountingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InventoryCountingItem>()
                .HasOne(item => item.Product)
                .WithMany(product => product.CountingItems)
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WarehouseClosing>()
                .HasIndex(closing => closing.DocumentNumber)
                .IsUnique();

            modelBuilder.Entity<WarehouseClosing>()
                .Property(closing => closing.DocumentNumber)
                .HasMaxLength(40);

            modelBuilder.Entity<WarehouseClosing>()
                .Property(closing => closing.ClosingDateShamsi)
                .HasMaxLength(20);

            modelBuilder.Entity<WarehouseClosing>()
                .HasOne(closing => closing.Warehouse)
                .WithMany(warehouse => warehouse.Closings)
                .HasForeignKey(closing => closing.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WarehouseClosingItem>()
                .Property(item => item.ClosingQuantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<WarehouseClosingItem>()
                .Property(item => item.OpeningQuantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<WarehouseClosingItem>()
                .HasOne(item => item.WarehouseClosing)
                .WithMany(closing => closing.Items)
                .HasForeignKey(item => item.WarehouseClosingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WarehouseClosingItem>()
                .HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryOpeningBalanceLedger>()
                .HasIndex(item => new { item.WarehouseId, item.ProductId, item.PeriodYear })
                .IsUnique();

            modelBuilder.Entity<InventoryOpeningBalanceLedger>()
                .Property(item => item.Quantity)
                .HasPrecision(18, 3);

            modelBuilder.Entity<InventoryOpeningBalanceLedger>()
                .HasOne(item => item.Warehouse)
                .WithMany(warehouse => warehouse.OpeningLedgers)
                .HasForeignKey(item => item.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryOpeningBalanceLedger>()
                .HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryOpeningBalanceLedger>()
                .HasOne(item => item.WarehouseClosing)
                .WithMany()
                .HasForeignKey(item => item.WarehouseClosingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AuditLog>()
                .Property(item => item.UserId)
                .HasMaxLength(100);

            modelBuilder.Entity<AuditLog>()
                .Property(item => item.Action)
                .HasMaxLength(20);

            modelBuilder.Entity<AuditLog>()
                .Property(item => item.TableName)
                .HasMaxLength(128);

            modelBuilder.Entity<AuditLog>()
                .Property(item => item.DateTime)
                .HasColumnType("datetimeoffset")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            modelBuilder.Entity<AuditLog>()
                .Property(item => item.UserIP)
                .HasMaxLength(64);

            modelBuilder.Entity<AuditLog>()
                .Property(item => item.UserAgent)
                .HasMaxLength(1024);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(item => new { item.TableName, item.DateTime });

            modelBuilder.Entity<ApplicationRole>()
                .Property(item => item.Description)
                .HasMaxLength(256);

            modelBuilder.Entity<ApplicationRole>()
                .Property(item => item.DataAccessScope)
                .HasMaxLength(32)
                .HasDefaultValue(RoleDataAccessScope.Department);

            modelBuilder.Entity<Permission>()
                .HasKey(item => item.Key);

            modelBuilder.Entity<Permission>()
                .Property(item => item.Key)
                .HasMaxLength(128);

            modelBuilder.Entity<Permission>()
                .Property(item => item.DisplayName)
                .HasMaxLength(128);

            modelBuilder.Entity<Permission>()
                .Property(item => item.Category)
                .HasMaxLength(64);

            modelBuilder.Entity<Permission>()
                .Property(item => item.Description)
                .HasMaxLength(256);

            modelBuilder.Entity<Permission>()
                .HasData(Services.Security.PermissionCatalog.CorePermissions);

            modelBuilder.Entity<RolePermission>()
                .HasIndex(item => new { item.RoleId, item.PermissionKey })
                .IsUnique();

            modelBuilder.Entity<RolePermission>()
                .HasOne(item => item.Role)
                .WithMany()
                .HasForeignKey(item => item.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(item => item.Permission)
                .WithMany()
                .HasForeignKey(item => item.PermissionKey)
                .HasPrincipalKey(item => item.Key)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Vendor>()
                .HasIndex(item => item.Name);

            modelBuilder.Entity<Vendor>()
                .HasIndex(item => item.EconomicCode);

            modelBuilder.Entity<Vendor>()
                .Property(item => item.Name)
                .HasMaxLength(150);

            modelBuilder.Entity<Vendor>()
                .Property(item => item.EconomicCode)
                .HasMaxLength(50);

            modelBuilder.Entity<Vendor>()
                .Property(item => item.NationalId)
                .HasMaxLength(20);

            modelBuilder.Entity<Vendor>()
                .Property(item => item.Phone)
                .HasMaxLength(20);

            modelBuilder.Entity<Vendor>()
                .Property(item => item.Address)
                .HasMaxLength(300);

            modelBuilder.Entity<Employer>()
                .HasIndex(item => item.Name);

            modelBuilder.Entity<Employer>()
                .HasIndex(item => item.ContractNumber);

            modelBuilder.Entity<Employer>()
                .Property(item => item.Name)
                .HasMaxLength(150);

            modelBuilder.Entity<Employer>()
                .Property(item => item.ContractNumber)
                .HasMaxLength(50);

            modelBuilder.Entity<Employer>()
                .Property(item => item.Phone)
                .HasMaxLength(20);

            modelBuilder.Entity<Employer>()
                .Property(item => item.Address)
                .HasMaxLength(300);

            modelBuilder.Entity<Warehouse>().HasData(
                new Warehouse
                {
                    Id = 1,
                    Code = "WH-MAIN",
                    Name = "انبار مرکزی",
                    Location = "ستاد",
                    IsActive = true,
                    IsClosed = false,
                    CreatedAt = new DateTime(2026, 1, 1)
                }
            );
        }
    }
}
