namespace EVMTracker.Messaging;

public interface IOutboxBatchStore
{
    Task<IReadOnlyList<OutboxDispatchItem>> GetPendingAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkPublishedAsync(
        Guid messageId,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid messageId,
        string failureMessage,
        DateTimeOffset nextAttemptAt,
        bool deadLetter,
        CancellationToken cancellationToken);
}
