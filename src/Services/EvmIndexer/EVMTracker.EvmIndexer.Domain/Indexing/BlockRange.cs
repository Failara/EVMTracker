namespace EVMTracker.EvmIndexer.Domain.Indexing;

public sealed record BlockRange(long FromBlock, long ToBlock)
{
    public static BlockRange Create(long fromBlock, long safeHead, int requestedSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fromBlock);

        if (safeHead < fromBlock)
        {
            throw new ArgumentOutOfRangeException(
                nameof(safeHead),
                "The safe head cannot precede the starting block.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestedSize, 0);

        long requestedEnd = fromBlock > long.MaxValue - requestedSize + 1L
            ? long.MaxValue
            : fromBlock + requestedSize - 1L;
        return new BlockRange(fromBlock, Math.Min(requestedEnd, safeHead));
    }

    public static long CalculateSafeHead(long chainHead, int confirmationDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chainHead);
        ArgumentOutOfRangeException.ThrowIfNegative(confirmationDepth);

        return chainHead - confirmationDepth;
    }
}
