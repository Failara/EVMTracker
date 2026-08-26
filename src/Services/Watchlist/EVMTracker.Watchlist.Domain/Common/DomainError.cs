namespace EVMTracker.Watchlist.Domain.Common;

public sealed record DomainError
{
    public static DomainError None { get; } = new(string.Empty, string.Empty);

    public DomainError(string code, string description)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(description);

        Code = code;
        Description = description;
    }

    public string Code { get; }

    public string Description { get; }
}
