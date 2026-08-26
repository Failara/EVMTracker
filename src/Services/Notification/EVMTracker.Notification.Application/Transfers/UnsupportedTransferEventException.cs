namespace EVMTracker.Notification.Application.Transfers;

public sealed class UnsupportedTransferEventException(string message) : Exception(message);
