namespace EVMTracker.TelegramGateway.Application.Commands;

public sealed record TelegramCommandRequest(
    long TelegramUserId,
    long TelegramChatId,
    string Text,
    Guid CorrelationId);
