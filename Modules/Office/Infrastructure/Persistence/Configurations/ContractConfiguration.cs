using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => item.ContractNumber).IsUnique();
        builder.HasIndex(item => item.ClientName);
        builder.HasIndex(item => item.Status);
        builder.HasIndex(item => item.AssignedUserId);
        builder.HasIndex(item => item.IsArchived);
        builder.HasIndex(item => item.CustomerId);
        builder.HasIndex(item => item.ContractType);
        builder.HasIndex(item => item.StartDate);
        builder.HasIndex(item => item.EndDate);

        builder.Property(item => item.ContractNumber).HasMaxLength(60).IsRequired();
        builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ContractType).HasMaxLength(60);
        builder.Property(item => item.ClientName).HasMaxLength(150).IsRequired();
        builder.Property(item => item.ClientPhone).HasMaxLength(20);
        builder.Property(item => item.ClientAddress).HasMaxLength(300);
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.TermsAndConditions).HasColumnType("ntext");
        builder.Property(item => item.ContractBodyHtml).HasColumnType("ntext");
        builder.Property(item => item.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(item => item.PaidAmount).HasColumnType("decimal(18,2)");
        builder.Property(item => item.RemainingBalance).HasColumnType("decimal(18,2)").HasComputedColumnSql("TotalAmount - PaidAmount");
        builder.Property(item => item.PaymentStatus).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(40).IsRequired();
        builder.Property(item => item.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.ExpiryNoticeDays).HasDefaultValue(30);

        builder.HasOne(item => item.Customer)
            .WithMany(customer => customer.Contracts)
            .HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(item => item.AssignedUser)
            .WithMany()
            .HasForeignKey(item => item.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.CreatedByUser)
            .WithMany()
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.ContractTemplate)
            .WithMany(template => template.Contracts)
            .HasForeignKey(item => item.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(item => item.Payments)
            .WithOne(payment => payment.Contract)
            .HasForeignKey(payment => payment.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(item => item.Attachments)
            .WithOne(attachment => attachment.Contract)
            .HasForeignKey(attachment => attachment.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(item => item.AuditLogs)
            .WithOne(al => al.Contract)
            .HasForeignKey(al => al.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}