using EVMTracker.Watchlist.Domain.Common;

namespace EVMTracker.Watchlist.Domain.TrackedAddresses;

public static class TrackedAddressErrors
{
    public static DomainError InvalidTelegramUserId { get; } = new(
        "TrackedAddress.InvalidTelegramUserId",
        "The Telegram user ID must be a positive integer.");

    public static DomainError InvalidTelegramChatId { get; } = new(
        "TrackedAddress.InvalidTelegramChatId",
        "The Telegram chat ID cannot be zero.");

    public static DomainError NetworkRequired { get; } = new(
        "TrackedAddress.NetworkRequired",
        "At least one EVM network must be enabled.");

    public static DomainError DuplicateNetwork { get; } = new(
        "TrackedAddress.DuplicateNetwork",
        "The same EVM network cannot be enabled more than once.");

    public static DomainError AlreadyTracked { get; } = new(
        "TrackedAddress.AlreadyTracked",
        "The Telegram user already tracks this address on the selected EVM network.");

    public static DomainError NetworkAlreadyEnabled { get; } = new(
        "TrackedAddress.NetworkAlreadyEnabled",
        "The EVM network is already enabled for this address.");

    public static DomainError NetworkNotEnabled { get; } = new(
        "TrackedAddress.NetworkNotEnabled",
        "The EVM network is not enabled for this address.");

    public static DomainError LastNetworkCannotBeDisabled { get; } = new(
        "TrackedAddress.LastNetworkCannotBeDisabled",
        "An active tracked address must have at least one enabled EVM network.");

    public static DomainError LabelTooLong { get; } = new(
        "TrackedAddress.LabelTooLong",
        $"A tracked-address label cannot exceed {TrackedAddress.MaximumLabelLength} characters.");

    public static DomainError Unauthorized { get; } = new(
        "TrackedAddress.Unauthorized",
        "Only the Telegram user who owns a tracked address may modify it.");

    public static DomainError Inactive { get; } = new(
        "TrackedAddress.Inactive",
        "An inactive tracked address cannot be modified.");

    public static DomainError AlreadyRemoved { get; } = new(
        "TrackedAddress.AlreadyRemoved",
        "The tracked address has already been removed.");
}
