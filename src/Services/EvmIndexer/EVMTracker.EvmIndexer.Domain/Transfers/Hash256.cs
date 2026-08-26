using System.Buffers;

namespace EVMTracker.EvmIndexer.Domain.Transfers;

public sealed record Hash256
{
    private const int HashLength = 66;
    private static readonly SearchValues<char> HexCharacters =
        SearchValues.Create("0123456789abcdefABCDEF");

    private Hash256(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? value, out Hash256? hash)
    {
        hash = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = value.Trim();
        if (candidate.Length == HashLength &&
            candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            !candidate.AsSpan(2).ContainsAnyExcept(HexCharacters))
        {
            hash = new Hash256("0x" + candidate.AsSpan(2).ToString().ToLowerInvariant());
            return true;
        }

        return false;
    }

    public override string ToString() => Value;
}
