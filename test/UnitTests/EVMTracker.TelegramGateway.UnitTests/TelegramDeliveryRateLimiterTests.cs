using EVMTracker.TelegramGateway.Infrastructure.Configuration;
using EVMTracker.TelegramGateway.Infrastructure.Telegram;
using Microsoft.Extensions.Options;

namespace EVMTracker.TelegramGateway.UnitTests;

public sealed class TelegramDeliveryRateLimiterTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GlobalReservationsAreSpacedByConfiguredThroughput()
    {
        TelegramDeliveryRateLimiter limiter = CreateLimiter();

        TimeSpan first = limiter.Reserve(1, Now);
        TimeSpan second = limiter.Reserve(2, Now);

        Assert.Equal(TimeSpan.Zero, first);
        Assert.Equal(TimeSpan.FromSeconds(1d / 30), second);
    }

    [Fact]
    public void SameChatReservationsRespectPerChatThroughput()
    {
        TelegramDeliveryRateLimiter limiter = CreateLimiter();

        TimeSpan first = limiter.Reserve(1, Now);
        TimeSpan second = limiter.Reserve(1, Now);

        Assert.Equal(TimeSpan.Zero, first);
        Assert.Equal(TimeSpan.FromSeconds(1), second);
    }

    private static TelegramDeliveryRateLimiter CreateLimiter() =>
        new(
            Options.Create(
                new TelegramDeliveryOptions
                {
                    GlobalMessagesPerSecond = 30,
                    PerChatMessagesPerSecond = 1,
                }),
            TimeProvider.System);
}
