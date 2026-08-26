namespace EVMTracker.Watchlist.Domain.Common;

public sealed class Result
{
    private Result(bool isSuccess, DomainError error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public DomainError Error { get; }

    public static Result Success() => new(true, DomainError.None);

    public static Result Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error == DomainError.None)
        {
            throw new ArgumentException("A failure result requires an error.", nameof(error));
        }

        return new Result(false, error);
    }

    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error == DomainError.None)
        {
            throw new ArgumentException("A failure result requires an error.", nameof(error));
        }

        return new Result<T>(error);
    }
}

public sealed class Result<T>
{
    private readonly T? _value;

    internal Result(T value)
    {
        _value = value;
        IsSuccess = true;
        Error = DomainError.None;
    }

    internal Result(DomainError error)
    {
        _value = default;
        IsSuccess = false;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public DomainError Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"A failed result has no value. Error: {Error.Code}.");

}
