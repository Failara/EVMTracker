namespace EVMTracker.Notification.Application.Transfers;

public sealed record OrphanProcessingResult(
    int OrphanedNotifications,
    int CorrectionsCreated,
    int PendingAlertsCancelled);
