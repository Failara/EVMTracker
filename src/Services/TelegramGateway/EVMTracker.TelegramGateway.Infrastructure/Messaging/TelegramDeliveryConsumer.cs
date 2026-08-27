using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using EVMTracker.Contracts.Notifications;
using EVMTracker.Observability;
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

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Every terminal or transient delivery exception must update the failure metric before broker handling continues.")]
    public async Task Consume(ConsumeContext<NotificationIntegrationEventEnvelope> context)
    {
        NotificationIntegrationEventEnvelope envelope = context.Message;
        using Activity? activity = EVMTrackerTelemetry.StartIntegrationEventActivity(
            "consume",
            envelope.EventType,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.CausationId,
            ActivityKind.Consumer);
        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = envelope.CorrelationId,
                ["EventId"] = envelope.EventId,
            });
        try
        {
            TelegramDeliveryRequestedV1 request = Deserialize(envelope);
            TelegramDeliveryProcessingResult result = await processor.ProcessAsync(
                request,
                context.CancellationToken).ConfigureAwait(false);
            EVMTrackerTelemetry.RecordTelegramDelivery(result.ToString().ToLowerInvariant());
            activity?.SetTag("delivery.outcome", result.ToString());
            LogDeliveryCompleted(logger, request.EventId, result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            EVMTrackerTelemetry.RecordTelegramDelivery("failed");
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
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
