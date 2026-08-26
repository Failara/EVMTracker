namespace EVMTracker.TelegramGateway.Application.Delivery;

public abstract class TelegramDeliveryException(string message) : Exception(message);

public sealed class PermanentTelegramDeliveryException(string message)
    : TelegramDeliveryException(message);

public sealed class TelegramDeliveryAttemptsExhaustedException(string message)
    : TelegramDeliveryException(message);
