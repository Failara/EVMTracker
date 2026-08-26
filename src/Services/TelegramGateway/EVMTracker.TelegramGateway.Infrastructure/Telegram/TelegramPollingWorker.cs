using EVMTracker.TelegramGateway.Application.Commands;
using EVMTracker.TelegramGateway.Application.Delivery;
using EVMTracker.TelegramGateway.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace EVMTracker.TelegramGateway.Infrastructure.Telegram;

public sealed partial class TelegramPollingWorker(
    ITelegramBotClient botClient,
    TelegramCommandService commandService,
    ITelegramDeliveryRateLimiter rateLimiter,
    IOptions<TelegramBotOptions> options,
    ILogger<TelegramPollingWorker> logger) : BackgroundService
{
    private readonly TelegramBotOptions options = options.Value;
    private int? offset;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogPollingStarted(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Update[] updates = await botClient.GetUpdates(
                    offset: offset,
                    timeout: options.PollingTimeoutSeconds,
                    allowedUpdates: [UpdateType.Message],
                    cancellationToken: stoppingToken).ConfigureAwait(false);

                foreach (Update update in updates)
                {
                    offset = update.Id + 1;
                    await HandleUpdateAsync(update, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (ApiRequestException exception)
            {
                await LogAndDelayAsync(exception, stoppingToken).ConfigureAwait(false);
            }
            catch (RequestException exception)
            {
                await LogAndDelayAsync(exception, stoppingToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                await LogAndDelayAsync(exception, stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(options.ErrorRetryDelay, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleUpdateAsync(
        Update update,
        CancellationToken cancellationToken)
    {
        Message? message = update.Message;
        if (message?.Text is null || message.From is null)
        {
            return;
        }

        Guid correlationId = Guid.CreateVersion7();
        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["TelegramUpdateId"] = update.Id,
            });

        string response = await commandService.HandleAsync(
            new TelegramCommandRequest(
                message.From.Id,
                message.Chat.Id,
                message.Text,
                correlationId),
            cancellationToken).ConfigureAwait(false);

        await rateLimiter.WaitAsync(message.Chat.Id, cancellationToken).ConfigureAwait(false);
        await botClient.SendMessage(
            message.Chat.Id,
            response,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task LogAndDelayAsync(
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogPollingFailed(logger, options.ErrorRetryDelay, exception);
        await Task.Delay(options.ErrorRetryDelay, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Telegram long polling started.")]
    private static partial void LogPollingStarted(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Telegram polling failed; retrying after {RetryDelay}.")]
    private static partial void LogPollingFailed(
        ILogger logger,
        TimeSpan retryDelay,
        Exception exception);
}
