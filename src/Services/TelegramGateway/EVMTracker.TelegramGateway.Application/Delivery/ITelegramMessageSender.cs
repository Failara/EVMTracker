namespace EVMTracker.TelegramGateway.Application.Delivery;

public interface ITelegramMessageSender
{
    Task<TelegramSendResult> SendAsync(
        long chatId,
        string message,
        string parseMode,
        CancellationToken cancellationToken);
}
