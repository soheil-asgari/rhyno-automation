using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Office.Infrastructure.Persistence.Configurations;

public sealed class LetterConfiguration : IEntityTypeConfiguration<Letter>
{
    public void Configure(EntityTypeBuilder<Letter> builder)
    {
            builder
                            .HasOne(l => l.Sender)
                            .WithMany()
                            .HasForeignKey(l => l.SenderId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(l => l.Receiver)
                            .WithMany()
                            .HasForeignKey(l => l.ReceiverId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .HasOne(l => l.FinalReceiver)
                            .WithMany()
                            .HasForeignKey(l => l.FinalReceiverId)
                            .OnDelete(DeleteBehavior.SetNull);

            builder
                            .HasOne(l => l.ReplyToLetter)
                            .WithMany()
                            .HasForeignKey(l => l.ReplyToLetterId)
                            .OnDelete(DeleteBehavior.Restrict);

            builder
                            .Property(item => item.DocumentType)
                            .HasMaxLength(60);

            builder
                            .Property(item => item.WorkflowStatus)
                            .HasMaxLength(30)
                            .HasDefaultValue(WorkflowStatus.Sent);
    }
}
