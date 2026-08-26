using EVMTracker.Watchlist.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.Watchlist.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.EventType)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(message => message.EventVersion)
            .IsRequired();
        builder.Property(message => message.OccurredAtUtc)
            .IsRequired();
        builder.Property(message => message.CorrelationId)
            .IsRequired();
        builder.Property(message => message.Payload)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(message => message.DeliveryAttempts)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(message => message.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(
                message => new
                {
                    message.ProcessedAtUtc,
                    message.DeadLetteredAtUtc,
                    message.NextAttemptAtUtc,
                })
            .HasDatabaseName("ix_outbox_messages_pending");
    }
}
