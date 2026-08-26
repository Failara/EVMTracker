namespace EVMTracker.Notification.Application.Transfers;

public sealed record TransferProcessingResult(
    int MatchedSubscriptions,
    int CreatedNotifications);
