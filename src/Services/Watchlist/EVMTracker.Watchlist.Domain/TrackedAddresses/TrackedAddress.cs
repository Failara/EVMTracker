using EVMTracker.Watchlist.Domain.Common;
using EVMTracker.Watchlist.Domain.ValueObjects;

namespace EVMTracker.Watchlist.Domain.TrackedAddresses;

public sealed class TrackedAddress
{
    public const int MaximumLabelLength = 64;

    private readonly HashSet<ChainId> _enabledNetworks;

    private TrackedAddress(
        Guid id,
        Address address,
        long telegramUserId,
        long telegramChatId,
        string? label,
        IEnumerable<ChainId> enabledNetworks,
        DateTimeOffset createdAtUtc,
        bool isActive)
    {
        Id = id;
        Address = address;
        TelegramUserId = telegramUserId;
        TelegramChatId = telegramChatId;
        Label = label;
        _enabledNetworks = [.. enabledNetworks];
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public Address Address { get; }

    public long TelegramUserId { get; }

    public long TelegramChatId { get; }

    public string? Label { get; private set; }

    public IReadOnlyCollection<ChainId> EnabledNetworks =>
        _enabledNetworks.OrderBy(chainId => chainId.Value).ToArray();

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static Result<TrackedAddress> Create(
        Address address,
        long telegramUserId,
        IEnumerable<ChainId>? enabledNetworks,
        string? label = null,
        long? telegramChatId = null)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (telegramUserId <= 0)
        {
            return Result.Failure<TrackedAddress>(TrackedAddressErrors.InvalidTelegramUserId);
        }

        long resolvedTelegramChatId = telegramChatId ?? telegramUserId;
        if (resolvedTelegramChatId == 0)
        {
            return Result.Failure<TrackedAddress>(TrackedAddressErrors.InvalidTelegramChatId);
        }

        Result<string?> labelResult = NormalizeLabel(label);
        if (labelResult.IsFailure)
        {
            return Result.Failure<TrackedAddress>(labelResult.Error);
        }

        if (enabledNetworks is null)
        {
            return Result.Failure<TrackedAddress>(TrackedAddressErrors.NetworkRequired);
        }

        ChainId[] networks = enabledNetworks.ToArray();
        if (networks.Length == 0 || networks.Any(network => network is null))
        {
            return Result.Failure<TrackedAddress>(TrackedAddressErrors.NetworkRequired);
        }

        HashSet<ChainId> uniqueNetworks = [.. networks];
        if (uniqueNetworks.Count != networks.Length)
        {
            return Result.Failure<TrackedAddress>(TrackedAddressErrors.DuplicateNetwork);
        }

        return Result.Success(
            new TrackedAddress(
                Guid.CreateVersion7(),
                address,
                telegramUserId,
                resolvedTelegramChatId,
                labelResult.Value,
                uniqueNetworks,
                DateTimeOffset.UtcNow,
                true));
    }

    internal static TrackedAddress Rehydrate(
        Guid id,
        Address address,
        long telegramUserId,
        long telegramChatId,
        string? label,
        IEnumerable<ChainId> enabledNetworks,
        DateTimeOffset createdAtUtc,
        bool isActive) =>
        new(
            id,
            address,
            telegramUserId,
            telegramChatId,
            label,
            enabledNetworks,
            createdAtUtc,
            isActive);

    public Result Rename(long requestingTelegramUserId, string? label)
    {
        Result modifiableResult = EnsureModifiableBy(requestingTelegramUserId);
        if (modifiableResult.IsFailure)
        {
            return modifiableResult;
        }

        Result<string?> labelResult = NormalizeLabel(label);
        if (labelResult.IsFailure)
        {
            return Result.Failure(labelResult.Error);
        }

        Label = labelResult.Value;
        return Result.Success();
    }

    public Result EnableNetwork(long requestingTelegramUserId, ChainId chainId)
    {
        ArgumentNullException.ThrowIfNull(chainId);

        Result modifiableResult = EnsureModifiableBy(requestingTelegramUserId);
        if (modifiableResult.IsFailure)
        {
            return modifiableResult;
        }

        if (!_enabledNetworks.Add(chainId))
        {
            return Result.Failure(TrackedAddressErrors.NetworkAlreadyEnabled);
        }

        return Result.Success();
    }

    public Result DisableNetwork(long requestingTelegramUserId, ChainId chainId)
    {
        ArgumentNullException.ThrowIfNull(chainId);

        Result modifiableResult = EnsureModifiableBy(requestingTelegramUserId);
        if (modifiableResult.IsFailure)
        {
            return modifiableResult;
        }

        if (!_enabledNetworks.Contains(chainId))
        {
            return Result.Failure(TrackedAddressErrors.NetworkNotEnabled);
        }

        if (_enabledNetworks.Count == 1)
        {
            return Result.Failure(TrackedAddressErrors.LastNetworkCannotBeDisabled);
        }

        _enabledNetworks.Remove(chainId);
        return Result.Success();
    }

    public Result Remove(long requestingTelegramUserId)
    {
        if (requestingTelegramUserId != TelegramUserId)
        {
            return Result.Failure(TrackedAddressErrors.Unauthorized);
        }

        if (!IsActive)
        {
            return Result.Failure(TrackedAddressErrors.AlreadyRemoved);
        }

        IsActive = false;
        return Result.Success();
    }

    public bool IsEnabledOn(ChainId chainId)
    {
        ArgumentNullException.ThrowIfNull(chainId);

        return IsActive && _enabledNetworks.Contains(chainId);
    }

    private static Result<string?> NormalizeLabel(string? label)
    {
        string? normalizedLabel = string.IsNullOrWhiteSpace(label)
            ? null
            : label.Trim();

        return normalizedLabel?.Length > MaximumLabelLength
            ? Result.Failure<string?>(TrackedAddressErrors.LabelTooLong)
            : Result.Success<string?>(normalizedLabel);
    }

    private Result EnsureModifiableBy(long requestingTelegramUserId)
    {
        if (requestingTelegramUserId != TelegramUserId)
        {
            return Result.Failure(TrackedAddressErrors.Unauthorized);
        }

        return IsActive
            ? Result.Success()
            : Result.Failure(TrackedAddressErrors.Inactive);
    }
}
