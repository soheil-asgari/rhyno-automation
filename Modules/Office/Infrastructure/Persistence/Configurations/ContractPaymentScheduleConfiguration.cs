using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class ContractPaymentScheduleConfiguration : IEntityTypeConfiguration<ContractPaymentSchedule>
{
    public void Configure(EntityTypeBuilder<ContractPaymentSchedule> builder)
    {
        builder.ToTable("ContractPaymentSchedules");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScheduleType).HasMaxLength(20).IsRequired();
        builder.Property(s => s.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(s => s.CreatedByUserId).HasMaxLength(450).IsRequired();

        builder.HasIndex(s => s.ContractId).IsUnique();

        builder.HasOne(s => s.Contract)
            .WithMany()
            .HasForeignKey(s => s.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.CreatedByUser)
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Installments)
            .WithOne(i => i.Schedule)
            .HasForeignKey(i => i.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractPaymentScheduleInstallmentConfiguration : IEntityTypeConfiguration<ContractPaymentScheduleInstallment>
{
    public void Configure(EntityTypeBuilder<ContractPaymentScheduleInstallment> builder)
    {
        builder.ToTable("ContractPaymentScheduleInstallments");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.Percentage).HasColumnType("decimal(5,2)");
        builder.Property(i => i.Description).HasMaxLength(300);
        builder.Property(i => i.Status).HasMaxLength(20).IsRequired();
        builder.Property(i => i.PaidAmount).HasColumnType("decimal(18,2)");

        builder.HasIndex(i => i.ScheduleId);
        builder.HasIndex(i => i.DueDate);

        builder.HasOne(i => i.Payment)
            .WithMany()
            .HasForeignKey(i => i.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}