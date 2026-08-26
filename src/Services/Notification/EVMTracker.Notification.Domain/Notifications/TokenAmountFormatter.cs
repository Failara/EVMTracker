using System.Globalization;
using System.Numerics;

namespace EVMTracker.Notification.Domain.Notifications;

public static class TokenAmountFormatter
{
    public static string Format(string rawValue, int? decimals)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);
        if (!BigInteger.TryParse(
                rawValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out BigInteger value) ||
            value < BigInteger.Zero)
        {
            throw new ArgumentException(
                "The token value must be an unsigned base-10 integer.",
                nameof(rawValue));
        }

        if (decimals is null)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (decimals is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decimals),
                "Token decimals must fit the ERC-20 uint8 range.");
        }

        if (decimals == 0)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        string digits = value.ToString(CultureInfo.InvariantCulture)
            .PadLeft(decimals.Value + 1, '0');
        int decimalPoint = digits.Length - decimals.Value;
        string integerPart = digits[..decimalPoint];
        string fractionalPart = digits[decimalPoint..].TrimEnd('0');
        return fractionalPart.Length == 0
            ? integerPart
            : $"{integerPart}.{fractionalPart}";
    }
}
