using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class ContractPaymentConfiguration : IEntityTypeConfiguration<ContractPayment>
{
    public void Configure(EntityTypeBuilder<ContractPayment> builder)
    {
        builder.ToTable("ContractPayments");

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => item.ContractId);
        builder.HasIndex(item => item.PaymentDate);

        builder.Property(item => item.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(item => item.PaymentMethod).HasMaxLength(60);
        builder.Property(item => item.ReferenceNumber).HasMaxLength(120);
        builder.Property(item => item.Notes).HasMaxLength(500);
        builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
    }
}