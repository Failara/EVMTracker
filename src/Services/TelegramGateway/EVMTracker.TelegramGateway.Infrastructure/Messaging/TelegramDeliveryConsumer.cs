using System.Text.Json;
using EVMTracker.Contracts.Notifications;
using EVMTracker.TelegramGateway.Application.Delivery;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EVMTracker.TelegramGateway.Infrastructure.Messaging;

internal sealed partial class TelegramDeliveryConsumer(
    TelegramDeliveryProcessor processor,
    ILogger<TelegramDeliveryConsumer> logger)
    : IConsumer<NotificationIntegrationEventEnvelope>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task Consume(ConsumeContext<NotificationIntegrationEventEnvelope> context)
    {
        TelegramDeliveryRequestedV1 request = Deserialize(context.Message);
        TelegramDeliveryProcessingResult result = await processor.ProcessAsync(
            request,
            context.CancellationToken).ConfigureAwait(false);
        LogDeliveryCompleted(logger, request.EventId, result);
    }

    private static TelegramDeliveryRequestedV1 Deserialize(
        NotificationIntegrationEventEnvelope envelope)
    {
        if (envelope.EventType != NotificationEventTypes.TelegramDeliveryRequestedV1 ||
            envelope.EventVersion != 1)
        {
            throw new JsonException(
                $"Notification event '{envelope.EventType}' version {envelope.EventVersion} is unsupported.");
        }

        TelegramDeliveryRequestedV1? request =
            envelope.Payload.Deserialize<TelegramDeliveryRequestedV1>(SerializerOptions);
        if (request is null ||
            request.EventId != envelope.EventId ||
            request.EventType != envelope.EventType ||
            request.EventVersion != envelope.EventVersion ||
            request.NotificationId == Guid.Empty ||
            request.TelegramChatId == 0 ||
            string.IsNullOrWhiteSpace(request.Message) ||
            request.Message.Length > 4096 ||
            string.IsNullOrWhiteSpace(request.ParseMode))
        {
            throw new JsonException(
                $"The payload metadata for '{envelope.EventType}' does not match its envelope.");
        }

        return request;
    }

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "Telegram delivery {EventId} completed with result {Result}.")]
    private static partial void LogDeliveryCompleted(
        ILogger logger,
        Guid eventId,
        TelegramDeliveryProcessingResult result);
}
