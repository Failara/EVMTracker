using EVMTracker.Notification.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.Notification.Infrastructure.Persistence.Configurations;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(message => message.EventId);
        builder.Property(message => message.EventType).HasMaxLength(200).IsRequired();
        builder.HasIndex(message => message.ProcessedAtUtc)
            .HasDatabaseName("ix_inbox_messages_processed_at_utc");
    }
}
