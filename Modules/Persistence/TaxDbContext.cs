using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Modules.Persistence.Entities;
using OfficeAutomation.Modules.Persistence.Configurations;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Modules.Persistence;

/// <summary>
/// Database context for the Tax &amp; Invoice Persistence module.
/// Manages InvoiceRecord, InvoiceLineItem, Seller, Buyer, and TaxLog entities.
/// </summary>
public sealed class TaxDbContext : ModularDbContext
{
    public TaxDbContext(
        DbContextOptions<TaxDbContext> options,
        ITenantIsolationService? tenantIsolationService = null)
        : base(options, tenantIsolationService)
    {
    }

    public DbSet<InvoiceRecord> InvoiceRecords => Set<InvoiceRecord>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<Buyer> Buyers => Set<Buyer>();
    public DbSet<TaxLog> TaxLogs => Set<TaxLog>();
    public DbSet<MowadiyanSubmission> MowadiyanSubmissions => Set<MowadiyanSubmission>();
    public DbSet<MowadiyanSettings> MowadiyanSettings => Set<MowadiyanSettings>();
    public DbSet<InvoiceReconciliationResult> InvoiceReconciliationResults => Set<InvoiceReconciliationResult>();
    public DbSet<ReconciliationDiscrepancy> ReconciliationDiscrepancies => Set<ReconciliationDiscrepancy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyTenantSchema(modelBuilder);

        modelBuilder.ApplyConfiguration(new InvoiceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceLineItemConfiguration());
        modelBuilder.ApplyConfiguration(new SellerConfiguration());
        modelBuilder.ApplyConfiguration(new BuyerConfiguration());
        modelBuilder.ApplyConfiguration(new TaxLogConfiguration());
        modelBuilder.ApplyConfiguration(new MowadiyanSubmissionConfiguration());
        modelBuilder.ApplyConfiguration(new MowadiyanSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceReconciliationResultConfiguration());
        modelBuilder.ApplyConfiguration(new ReconciliationDiscrepancyConfiguration());

        // Full-text search index hint (requires SQL Server or PostgreSQL FTS extension).
        modelBuilder.Entity<InvoiceRecord>()
            .HasIndex(r => r.RawOcrText)
            .HasDatabaseName("IX_InvoiceRecord_RawOcrText")
            .IsUnique(false);

        // Composite index for common dashboard queries.
        modelBuilder.Entity<InvoiceRecord>()
            .HasIndex(r => new { r.Status, r.CreatedAt })
            .HasDatabaseName("IX_InvoiceRecord_Status_CreatedAt");

        modelBuilder.Entity<InvoiceRecord>()
            .HasIndex(r => r.InvoiceDate)
            .HasDatabaseName("IX_InvoiceRecord_InvoiceDate");

        modelBuilder.Entity<InvoiceRecord>()
            .HasIndex(r => r.SellerId)
            .HasDatabaseName("IX_InvoiceRecord_SellerId");

        modelBuilder.Entity<InvoiceRecord>()
            .HasIndex(r => r.BuyerId)
            .HasDatabaseName("IX_InvoiceRecord_BuyerId");
    }
}
