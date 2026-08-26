using EVMTracker.EvmIndexer.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EVMTracker.EvmIndexer.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.EventType).HasMaxLength(200).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(message => message.ProcessedAtUtc)
            .HasDatabaseName("ix_indexer_outbox_processed_at_utc");
    }
}
