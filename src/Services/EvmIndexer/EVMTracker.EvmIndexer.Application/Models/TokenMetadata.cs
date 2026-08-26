namespace EVMTracker.EvmIndexer.Application.Models;

public sealed record TokenMetadata(string Symbol, string? Name, int? Decimals)
{
    public static TokenMetadata Unknown(string tokenContract)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenContract);
        string suffix = tokenContract.Length >= 8 ? tokenContract[^6..] : tokenContract;
        return new TokenMetadata($"TOKEN-{suffix}", null, null);
    }
}
