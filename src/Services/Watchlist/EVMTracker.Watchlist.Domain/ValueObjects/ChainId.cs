using EVMTracker.Watchlist.Domain.Common;

namespace EVMTracker.Watchlist.Domain.ValueObjects;

public sealed record ChainId
{
    private ChainId(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public static Result<ChainId> Create(long value) => value > 0
        ? Result.Success(new ChainId(value))
        : Result.Failure<ChainId>(ChainIdErrors.Invalid);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
