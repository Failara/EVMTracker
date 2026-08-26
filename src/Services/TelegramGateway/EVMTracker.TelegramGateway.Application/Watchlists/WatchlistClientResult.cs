namespace EVMTracker.TelegramGateway.Application.Watchlists;

public sealed class WatchlistClientResult<T>
{
    private readonly T? value;

    internal WatchlistClientResult(T? value, WatchlistClientFailure? failure)
    {
        this.value = value;
        Failure = failure;
    }

    public bool IsSuccess => Failure is null;

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed result has no value.");

    public WatchlistClientFailure? Failure { get; }

}

public static class WatchlistClientResult
{
    public static WatchlistClientResult<T> Success<T>(T value) => new(value, null);

    public static WatchlistClientResult<T> Failed<T>(
        WatchlistClientError error,
        string detail) => new(default, new WatchlistClientFailure(error, detail));
}
