using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
            builder
                            .HasIndex(i => new { i.InvoiceNumber, i.InvoiceType })
                            .IsUnique()
                            .HasDatabaseName("IX_Invoice_Number_Type");

            builder
                            .HasIndex(i => new { i.InvoiceType, i.DateShamsi })
                            .HasDatabaseName("IX_Invoice_Type_DateShamsi");

            builder
                            .Property(i => i.InvoiceType)
                            .HasMaxLength(20);

            builder
                            .Property(i => i.DateShamsi)
                            .HasMaxLength(20);

            builder
                            .Property(i => i.PartyName)
                            .HasMaxLength(150);

            builder
                            .Property(i => i.NationalCodeOrEconomicId)
                            .HasMaxLength(30);

            builder
                            .Property(i => i.SubTotal)
                            .HasPrecision(18, 2);

            builder
                            .Property(i => i.VatAmount)
                            .HasPrecision(18, 2);

            builder
                            .Property(i => i.GrandTotal)
                            .HasPrecision(18, 2);

            builder
                            .Property(i => i.Notes)
                            .HasMaxLength(600);

            builder
                            .Property(i => i.WorkflowStatus)
                            .HasMaxLength(30)
                            .HasDefaultValue(WorkflowStatus.Draft);

            builder
                            .Property(i => i.RowVersion)
                            .IsRowVersion();

            builder
                            .Property(i => i.DeadlineDateShamsi)
                            .HasMaxLength(20);

            builder.Ignore(i => i.WarehouseReceipt);
            builder.Ignore(i => i.FollowUpEmployee);

            builder
                            .HasOne(i => i.Employer)
                            .WithMany(e => e.Invoices)
                            .HasForeignKey(i => i.EmployerId)
                            .OnDelete(DeleteBehavior.SetNull);

            builder
                            .Property(item => item.CreatedByUserId)
                            .HasMaxLength(450);

            builder.Ignore(item => item.CreatedByUser);
    }
}
