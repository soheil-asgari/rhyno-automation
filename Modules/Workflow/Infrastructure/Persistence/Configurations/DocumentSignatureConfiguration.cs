using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.Configurations;

public sealed class DocumentSignatureConfiguration : IEntityTypeConfiguration<DocumentSignature>
{
    public void Configure(EntityTypeBuilder<DocumentSignature> builder)
    {
        builder.HasIndex(item => new { item.DocumentType, item.DocumentId, item.SignedAt });
        builder.HasIndex(item => item.PayloadHash);
        builder.Property(item => item.DocumentType).HasMaxLength(60).IsRequired();
        builder.Property(item => item.SignerUserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.HashAlgorithm).HasMaxLength(50).HasDefaultValue("SHA256").IsRequired();
        builder.Property(item => item.SignatureKeyId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.SignatureValue).IsRequired();
        builder.Property(item => item.CertificateThumbprint).HasMaxLength(128);
        builder.Property(item => item.CertificateSubject).HasMaxLength(500);
        builder.Property(item => item.CanonicalPayload).IsRequired();
        builder.Property(item => item.SignerDisplayName).HasMaxLength(1000);
        builder.Property(item => item.SigningReason).HasMaxLength(1000);

        builder
            .HasOne(item => item.WorkflowInstance)
            .WithMany(item => item.DocumentSignatures)
            .HasForeignKey(item => item.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(item => item.WorkflowDecision)
            .WithMany(item => item.DocumentSignatures)
            .HasForeignKey(item => item.WorkflowDecisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(item => item.SignerUser)
            .WithMany()
            .HasForeignKey(item => item.SignerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
