using EVMTracker.Watchlist.Domain.Common;

namespace EVMTracker.Watchlist.Domain.ValueObjects;

public static class AddressErrors
{
    public static DomainError Required { get; } = new(
        "Address.Required",
        "An EVM address is required.");

    public static DomainError InvalidFormat { get; } = new(
        "Address.InvalidFormat",
        "An EVM address must start with 0x and contain exactly 40 hexadecimal characters.");

    public static DomainError InvalidChecksum { get; } = new(
        "Address.InvalidChecksum",
        "The mixed-case EVM address has an invalid EIP-55 checksum.");
}
