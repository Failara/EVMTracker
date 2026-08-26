using EVMTracker.TelegramGateway.Application.Delivery;
using EVMTracker.TelegramGateway.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace EVMTracker.TelegramGateway.Infrastructure.Telegram;

internal sealed class TelegramDeliveryRateLimiter(
    IOptions<TelegramDeliveryOptions> options,
    TimeProvider timeProvider) : ITelegramDeliveryRateLimiter
{
    private readonly object sync = new();
    private readonly TimeSpan globalSpacing = TimeSpan.FromSeconds(
        1d / options.Value.GlobalMessagesPerSecond);
    private readonly TimeSpan perChatSpacing = TimeSpan.FromSeconds(
        1d / options.Value.PerChatMessagesPerSecond);
    private readonly Dictionary<long, DateTimeOffset> nextChatPermit = [];
    private DateTimeOffset nextGlobalPermit;

    public async Task WaitAsync(long chatId, CancellationToken cancellationToken)
    {
        TimeSpan delay = Reserve(chatId, timeProvider.GetUtcNow());
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    internal TimeSpan Reserve(long chatId, DateTimeOffset now)
    {
        lock (sync)
        {
            DateTimeOffset chatPermit = nextChatPermit.GetValueOrDefault(chatId, now);
            DateTimeOffset permitAt = Max(now, Max(nextGlobalPermit, chatPermit));
            nextGlobalPermit = permitAt + globalSpacing;
            nextChatPermit[chatId] = permitAt + perChatSpacing;
            return permitAt - now;
        }
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;
}
