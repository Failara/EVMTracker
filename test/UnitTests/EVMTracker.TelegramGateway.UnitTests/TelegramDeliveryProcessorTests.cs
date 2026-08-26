using EVMTracker.Contracts.Notifications;
using EVMTracker.TelegramGateway.Application.Delivery;

namespace EVMTracker.TelegramGateway.UnitTests;

public sealed class TelegramDeliveryProcessorTests
{
    private static readonly TelegramDeliveryPolicy Policy = new(
        3,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(30),
        0.2);

    [Fact]
    public async Task RetryAfterIsHonoredBeforeSuccessfulRetry()
    {
        var store = new RecordingStore();
        var sender = new SequenceSender(
            TelegramSendResult.TransientFailure("Too many requests", TimeSpan.FromSeconds(7)),
            TelegramSendResult.Success(123));
        var delay = new RecordingDelay();
        TelegramDeliveryProcessor processor = CreateProcessor(store, sender, delay);

        TelegramDeliveryProcessingResult result = await processor.ProcessAsync(
            CreateRequest(),
            CancellationToken.None);

        Assert.Equal(TelegramDeliveryProcessingResult.Delivered, result);
        Assert.Equal(2, sender.SendCount);
        Assert.Equal([TimeSpan.FromSeconds(7)], delay.Delays);
        Assert.Equal(TelegramDeliveryStatus.Delivered, store.State.Status);
        Assert.Equal(2, store.Attempts.Count);
    }

    [Fact]
    public async Task TransientFailuresUseBoundedExponentialDelayWithJitter()
    {
        var store = new RecordingStore();
        var sender = new SequenceSender(
            TelegramSendResult.TransientFailure("Network unavailable"),
            TelegramSendResult.Success(123));
        var delay = new RecordingDelay();
        TelegramDeliveryRequestedV1 request = CreateRequest();
        TelegramDeliveryProcessor processor = CreateProcessor(store, sender, delay);

        await processor.ProcessAsync(request, CancellationToken.None);

        TimeSpan recorded = Assert.Single(delay.Delays);
        Assert.InRange(recorded, Policy.InitialRetryDelay, TimeSpan.FromSeconds(1.2));
    }

    [Fact]
    public async Task PermanentFailureIsPersistedAndPropagatedToBroker()
    {
        var store = new RecordingStore();
        var sender = new SequenceSender(
            TelegramSendResult.PermanentFailure("Bot was blocked by the user"));
        TelegramDeliveryProcessor processor = CreateProcessor(
            store,
            sender,
            new RecordingDelay());

        await Assert.ThrowsAsync<PermanentTelegramDeliveryException>(
            () => processor.ProcessAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(TelegramDeliveryStatus.PermanentlyFailed, store.State.Status);
        Assert.Equal(TelegramDeliveryAttemptOutcome.PermanentFailure, store.Attempts[0]);
    }

    [Fact]
    public async Task ExhaustedTransientFailuresBecomeTerminal()
    {
        var store = new RecordingStore();
        var sender = new SequenceSender(
            TelegramSendResult.TransientFailure("one"),
            TelegramSendResult.TransientFailure("two"),
            TelegramSendResult.TransientFailure("three"));
        var delay = new RecordingDelay();
        TelegramDeliveryProcessor processor = CreateProcessor(store, sender, delay);

        await Assert.ThrowsAsync<TelegramDeliveryAttemptsExhaustedException>(
            () => processor.ProcessAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(3, sender.SendCount);
        Assert.Equal(2, delay.Delays.Count);
        Assert.Equal(TelegramDeliveryStatus.PermanentlyFailed, store.State.Status);
    }

    [Fact]
    public async Task DeliveredDuplicateDoesNotCallTelegramAgain()
    {
        var store = new RecordingStore(TelegramDeliveryStatus.Delivered);
        var sender = new SequenceSender(TelegramSendResult.Success(456));
        TelegramDeliveryProcessor processor = CreateProcessor(
            store,
            sender,
            new RecordingDelay());

        TelegramDeliveryProcessingResult result = await processor.ProcessAsync(
            CreateRequest(),
            CancellationToken.None);

        Assert.Equal(TelegramDeliveryProcessingResult.AlreadyDelivered, result);
        Assert.Equal(0, sender.SendCount);
    }

    private static TelegramDeliveryProcessor CreateProcessor(
        ITelegramDeliveryStore store,
        ITelegramMessageSender sender,
        IDeliveryDelay delay) =>
        new(
            store,
            sender,
            new NoOpRateLimiter(),
            delay,
            Policy,
            TimeProvider.System);

    private static TelegramDeliveryRequestedV1 CreateRequest()
    {
        Guid eventId = Guid.Parse("01991ef2-1234-7d20-83ef-53cd755f09a7");
        return new TelegramDeliveryRequestedV1(
            eventId,
            new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero),
            eventId,
            84,
            "<b>Incoming transfer</b>",
            "HTML");
    }

    private sealed class RecordingStore : ITelegramDeliveryStore
    {
        public RecordingStore(
            TelegramDeliveryStatus initialStatus = TelegramDeliveryStatus.Pending)
        {
            State = new TelegramDeliveryState(Guid.Empty, initialStatus, 0);
        }

        public TelegramDeliveryState State { get; private set; }

        public List<TelegramDeliveryAttemptOutcome> Attempts { get; } = [];

        public Task<TelegramDeliveryState> RegisterAsync(
            TelegramDeliveryRequestedV1 request,
            DateTimeOffset registeredAtUtc,
            CancellationToken cancellationToken)
        {
            State = State with { EventId = request.EventId };
            return Task.FromResult(State);
        }

        public Task<int> BeginAttemptAsync(
            Guid eventId,
            DateTimeOffset attemptedAtUtc,
            CancellationToken cancellationToken)
        {
            State = State with { AttemptCount = State.AttemptCount + 1 };
            return Task.FromResult(State.AttemptCount);
        }

        public Task CompleteAttemptAsync(
            Guid eventId,
            int attemptNumber,
            TelegramDeliveryAttemptOutcome outcome,
            int? telegramMessageId,
            string? failureReason,
            TimeSpan? retryDelay,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken)
        {
            Attempts.Add(outcome);
            State = State with
            {
                Status = outcome switch
                {
                    TelegramDeliveryAttemptOutcome.Delivered =>
                        TelegramDeliveryStatus.Delivered,
                    TelegramDeliveryAttemptOutcome.TransientFailure =>
                        TelegramDeliveryStatus.RetryScheduled,
                    _ => TelegramDeliveryStatus.PermanentlyFailed,
                },
            };
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceSender(params TelegramSendResult[] results)
        : ITelegramMessageSender
    {
        private readonly Queue<TelegramSendResult> results = new(results);

        public int SendCount { get; private set; }

        public Task<TelegramSendResult> SendAsync(
            long chatId,
            string message,
            string parseMode,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed class RecordingDelay : IDeliveryDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpRateLimiter : ITelegramDeliveryRateLimiter
    {
        public Task WaitAsync(long chatId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
