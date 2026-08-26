using EVMTracker.Watchlist.Domain.Common;

namespace EVMTracker.Watchlist.UnitTests;

public sealed class ResultTests
{
    private static readonly DomainError ExpectedError = new(
        "Test.Failure",
        "Expected failure.");

    [Fact]
    public void SuccessfulGenericResultExposesValue()
    {
        Result<string> result = Result.Success("value");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("value", result.Value);
        Assert.Equal(DomainError.None, result.Error);
    }

    [Fact]
    public void FailedGenericResultThrowsWhenValueIsAccessed()
    {
        Result<string> result = Result.Failure<string>(ExpectedError);

        Assert.True(result.IsFailure);
        Assert.Equal(ExpectedError, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void FailureRequiresNonEmptyError()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(DomainError.None));
        Assert.Throws<ArgumentException>(() => Result.Failure<string>(DomainError.None));
    }
}
