namespace EVMTracker.EvmIndexer.Application.Abstractions;

public sealed class EvmRpcRangeLimitException : Exception
{
    public EvmRpcRangeLimitException(string message)
        : base(message)
    {
    }

    public EvmRpcRangeLimitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
