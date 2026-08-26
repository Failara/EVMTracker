using EVMTracker.TelegramGateway.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.TelegramGateway.Infrastructure.Persistence.Configurations;

internal sealed class TelegramDeliveryAttemptRecordConfiguration
    : IEntityTypeConfiguration<TelegramDeliveryAttemptRecord>
{
    public void Configure(EntityTypeBuilder<TelegramDeliveryAttemptRecord> builder)
    {
        builder.ToTable("telegram_delivery_attempts");
        builder.HasKey(attempt => new { attempt.EventId, attempt.AttemptNumber });
        builder.Property(attempt => attempt.Outcome)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(attempt => attempt.Error).HasMaxLength(2000);
        builder.HasOne<TelegramDeliveryRecord>()
            .WithMany()
            .HasForeignKey(attempt => attempt.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
