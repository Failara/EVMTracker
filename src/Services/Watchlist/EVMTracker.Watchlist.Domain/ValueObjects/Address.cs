using System.Text;
using EVMTracker.Watchlist.Domain.Common;
using EVMTracker.Watchlist.Domain.Cryptography;

namespace EVMTracker.Watchlist.Domain.ValueObjects;

public sealed record Address
{
    private const int AddressLength = 42;
    private const int HexLength = 40;

    private Address(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Address> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Address>(AddressErrors.Required);
        }

        string candidate = value.Trim();
        if (candidate.Length != AddressLength ||
            !candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<Address>(AddressErrors.InvalidFormat);
        }

        ReadOnlySpan<char> hexadecimal = candidate.AsSpan(2);
        bool hasLowercaseLetter = false;
        bool hasUppercaseLetter = false;

        foreach (char character in hexadecimal)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return Result.Failure<Address>(AddressErrors.InvalidFormat);
            }

            hasLowercaseLetter |= character is >= 'a' and <= 'f';
            hasUppercaseLetter |= character is >= 'A' and <= 'F';
        }

        string lowercaseHexadecimal = hexadecimal.ToString().ToLowerInvariant();
        string checksumAddress = ToChecksumAddress(lowercaseHexadecimal);

        if (hasLowercaseLetter &&
            hasUppercaseLetter &&
            !hexadecimal.SequenceEqual(checksumAddress.AsSpan(2)))
        {
            return Result.Failure<Address>(AddressErrors.InvalidChecksum);
        }

        return Result.Success(new Address(checksumAddress));
    }

    public override string ToString() => Value;

    private static string ToChecksumAddress(string lowercaseHexadecimal)
    {
        byte[] hash = Keccak256.Hash(Encoding.ASCII.GetBytes(lowercaseHexadecimal));
        Span<char> checksum = stackalloc char[AddressLength];
        checksum[0] = '0';
        checksum[1] = 'x';

        for (int index = 0; index < HexLength; index++)
        {
            char character = lowercaseHexadecimal[index];
            int hashNibble = index % 2 == 0
                ? hash[index / 2] >> 4
                : hash[index / 2] & 0x0F;

            checksum[index + 2] = character is >= 'a' and <= 'f' && hashNibble >= 8
                ? char.ToUpperInvariant(character)
                : character;
        }

        return new string(checksum);
    }
}
