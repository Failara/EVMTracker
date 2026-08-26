using System.Buffers;

namespace EVMTracker.EvmIndexer.Domain.Transfers;

public sealed record EvmAddress
{
    private const int AddressLength = 42;
    private static readonly SearchValues<char> HexCharacters =
        SearchValues.Create("0123456789abcdefABCDEF");

    private EvmAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? value, out EvmAddress? address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = value.Trim();
        if (candidate.Length == AddressLength &&
            candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            !candidate.AsSpan(2).ContainsAnyExcept(HexCharacters))
        {
            address = new EvmAddress("0x" + candidate.AsSpan(2).ToString().ToLowerInvariant());
            return true;
        }

        return false;
    }

    public override string ToString() => Value;
}
