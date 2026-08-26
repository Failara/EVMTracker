using EVMTracker.Watchlist.Domain.ValueObjects;

namespace EVMTracker.Watchlist.UnitTests;

public sealed class ChainIdTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(56)]
    [InlineData(137)]
    [InlineData(long.MaxValue)]
    public void CreateAcceptsPositiveValue(long value)
    {
        var result = ChainId.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void CreateRejectsNonPositiveValue(long value)
    {
        var result = ChainId.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal(ChainIdErrors.Invalid, result.Error);
    }

    [Fact]
    public void EqualChainIdsHaveValueEquality()
    {
        ChainId first = ChainId.Create(1).Value;
        ChainId second = ChainId.Create(1).Value;

        Assert.Equal(first, second);
    }
}
