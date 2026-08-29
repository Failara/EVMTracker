using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Numerics;
using System.Text;
using System.Text.Json;
using EVMTracker.Contracts;
using EVMTracker.Contracts.Notifications;
using EVMTracker.Contracts.Transfers;
using EVMTracker.Contracts.Watchlists;
using EVMTracker.EvmIndexer.Application.Decoding;
using EVMTracker.EvmIndexer.Application.Models;
using EVMTracker.EvmIndexer.Domain.Transfers;
using EVMTracker.EvmIndexer.Infrastructure.Configuration;
using EVMTracker.EvmIndexer.Infrastructure.Rpc;
using EVMTracker.Messaging.Publishing;
using EVMTracker.Notification.Application.Transfers;
using EVMTracker.Notification.Application.Watchlists;
using EVMTracker.Notification.Domain.Notifications;
using EVMTracker.TelegramGateway.Application.Delivery;
using Microsoft.Extensions.Options;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Xunit.Abstractions;

namespace EVMTracker.EvmIndexer.IntegrationTests;

[Collection(ContainerInfrastructureDefinition.Name)]
public sealed class AnvilTransferEndToEndTests(
    AnvilContainerFixture fixture,
    ITestOutputHelper output)
{
    private const long ChainId = 31337;
    private const string SenderAddress =
        "0xf39fd6e51aad88f6f4ce6ab8827279cfffb92266";
    private const string RecipientAddress =
        "0x3c44cdddb6a900fa2b585dd299e03d12fa4293bc";
    private const string TokenAddress =
        "0x1000000000000000000000000000000000000001";
    private const string OtherTokenAddress =
        "0x2000000000000000000000000000000000000002";
    private const string UntrackedAddress =
        "0x3000000000000000000000000000000000000003";

    [Fact]
    [Trait("Category", "ContainerIntegration")]
    public async Task RealEvmLogReachesFakeTelegramExactlyOnce()
    {
        var web3 = new Web3(fixture.RpcAddress.ToString());
        string[] accounts = await web3.Eth.Accounts.SendRequestAsync();
        Assert.Contains(SenderAddress, accounts, StringComparer.OrdinalIgnoreCase);

        await SetRuntimeCodeAsync(TokenAddress, BuildTransferEmitterRuntime());
        string transactionHash = await web3.Eth.Transactions.SendTransaction.SendRequestAsync(
            new TransactionInput
            {
                From = SenderAddress,
                To = TokenAddress,
                Gas = new HexBigInteger(250_000),
            });
        TransactionReceipt receipt = await WaitForReceiptAsync(web3, transactionHash);
        long blockNumber = checked((long)receipt.BlockNumber.Value);

        var options = Options.Create(new EvmIndexerOptions
        {
            RpcRequestTimeout = TimeSpan.FromSeconds(10),
            Networks =
            [
                new EvmNetworkOptions
                {
                    Name = "anvil",
                    ChainId = ChainId,
                    RpcUrl = fixture.RpcAddress.ToString(),
                    StartBlock = blockNumber,
                    ConfirmationDepth = 0,
                },
            ],
        });
        var source = new NethereumEvmLogSource(options);
        IReadOnlyList<RawEvmLog> logs = await source.GetTransferLogsAsync(
            ChainId,
            blockNumber,
            blockNumber,
            [SenderAddress],
            CancellationToken.None);
        RawEvmLog rawLog = Assert.Single(logs);
        TransferLogDecodeResult decoded = Erc20TransferLogDecoder.Decode(ChainId, rawLog);
        Assert.True(decoded.IsSuccess, decoded.Error);
        Erc20Transfer transfer = Assert.IsType<Erc20Transfer>(decoded.Transfer);
        Assert.Equal(new BigInteger(5_000_000), transfer.RawValue);

        DateTimeOffset occurredAt = new(2026, 8, 29, 1, 0, 0, TimeSpan.Zero);
        var observed = new Erc20TransferObservedV1(
            Guid.NewGuid(),
            occurredAt,
            ChainId,
            transfer.TokenContract.Value,
            transfer.FromAddress.Value,
            transfer.ToAddress.Value,
            transfer.RawValue.ToString(CultureInfo.InvariantCulture),
            "USDT",
            "Tether USD",
            6,
            transfer.TransactionHash.Value,
            transfer.BlockNumber,
            transfer.BlockHash.Value,
            transfer.LogIndex);
        IntegrationEventEnvelope envelope = IntegrationEventEnvelopeFactory.Create(observed);
        var transferEnvelope = new TransferIntegrationEventEnvelope(
            envelope.EventId,
            envelope.EventType,
            envelope.EventVersion,
            envelope.OccurredAt,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.Payload);

        var notificationStore = new CapturingNotificationStore();
        var notificationProcessor = new TransferEventProcessor(
            new SingleEntryWatchlistProjection(SenderAddress, occurredAt),
            notificationStore,
            new AnvilNetworkResolver());
        TransferProcessingResult notificationResult = await notificationProcessor.ProcessAsync(
            transferEnvelope,
            CancellationToken.None);
        PreparedTransferNotification prepared = Assert.Single(notificationStore.Notifications);

        var telegramStore = new InMemoryTelegramDeliveryStore();
        var sender = new CapturingTelegramSender();
        var telegramProcessor = new TelegramDeliveryProcessor(
            telegramStore,
            sender,
            new NoOpRateLimiter(),
            new NoOpDelay(),
            new TelegramDeliveryPolicy(
                3,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(10),
                0),
            TimeProvider.System);
        TelegramDeliveryProcessingResult deliveryResult = await telegramProcessor.ProcessAsync(
            prepared.DeliveryRequest,
            CancellationToken.None);

        Assert.Equal(1, notificationResult.CreatedNotifications);
        Assert.Equal(TransferDirection.Outgoing, prepared.Notification.Direction);
        Assert.Equal("5", prepared.Notification.DisplayAmount);
        Assert.Equal(TelegramDeliveryProcessingResult.Delivered, deliveryResult);
        Assert.Equal(TelegramDeliveryStatus.Delivered, telegramStore.State.Status);
        CapturedTelegramMessage message = Assert.Single(sender.Messages);
        Assert.Contains("5 USDT", message.Message, StringComparison.Ordinal);
        Assert.Contains("Outgoing", message.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ContainerIntegration")]
    public async Task RpcOutageFailsFastAndFreshWorkerClientRecoversAfterRestart()
    {
        NethereumEvmLogSource source = CreateLogSource();
        Assert.True(await source.GetHeadBlockNumberAsync(ChainId, CancellationToken.None) >= 0);

        await fixture.Container.StopAsync();
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => source.GetHeadBlockNumberAsync(
                ChainId,
                CancellationToken.None));
        }
        finally
        {
            await fixture.Container.StartAsync();
        }

        NethereumEvmLogSource restartedSource = CreateLogSource();
        Assert.True(await restartedSource.GetHeadBlockNumberAsync(
            ChainId,
            CancellationToken.None) >= 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(500)]
    [Trait("Category", "PerformanceProbe")]
    public async Task FilteredLogQueryScalesAcrossTrackedAddressCounts(int addressCount)
    {
        var web3 = new Web3(fixture.RpcAddress.ToString());
        await SetRuntimeCodeAsync(TokenAddress, BuildTransferEmitterRuntime());
        string transactionHash = await web3.Eth.Transactions.SendTransaction.SendRequestAsync(
            new TransactionInput
            {
                From = SenderAddress,
                To = TokenAddress,
                Gas = new HexBigInteger(250_000),
            });
        TransactionReceipt receipt = await WaitForReceiptAsync(web3, transactionHash);
        long blockNumber = checked((long)receipt.BlockNumber.Value);
        string[] trackedAddresses = Enumerable.Range(1, addressCount - 1)
            .Select(index => $"0x{index.ToString("x40", CultureInfo.InvariantCulture)}")
            .Prepend(SenderAddress)
            .ToArray();

        NethereumEvmLogSource source = CreateLogSource(TimeSpan.FromSeconds(10));
        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<RawEvmLog> logs = await source.GetTransferLogsAsync(
            ChainId,
            blockNumber,
            blockNumber,
            trackedAddresses,
            CancellationToken.None);
        stopwatch.Stop();

        RawEvmLog observed = Assert.Single(logs);
        Assert.Equal(transactionHash, observed.TransactionHash, ignoreCase: true);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            addressCount,
            elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            returnedLogs = logs.Count,
        }));
    }

    [Fact]
    [Trait("Category", "PerformanceProbe")]
    public async Task FilteredModelExcludesIrrelevantLogsComparedWithBroadScan()
    {
        const int irrelevantTransferCount = 50;
        var web3 = new Web3(fixture.RpcAddress.ToString());
        await SetRuntimeCodeAsync(TokenAddress, BuildTransferEmitterRuntime());
        TransactionReceipt trackedReceipt = await EmitTransferAsync(web3, TokenAddress);
        await SetRuntimeCodeAsync(
            OtherTokenAddress,
            BuildTransferEmitterRuntime(
                RecipientAddress,
                UntrackedAddress,
                1));
        TransactionReceipt? lastIrrelevantReceipt = null;
        for (int index = 0; index < irrelevantTransferCount; index++)
        {
            lastIrrelevantReceipt = await EmitTransferAsync(web3, OtherTokenAddress);
        }

        Assert.NotNull(lastIrrelevantReceipt);
        long fromBlock = checked((long)trackedReceipt.BlockNumber.Value);
        long toBlock = checked((long)lastIrrelevantReceipt.BlockNumber.Value);
        var broadFilter = new NewFilterInput
        {
            FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
            ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
            Topics = [Erc20TransferLogDecoder.TransferTopic],
        };
        var broadStopwatch = Stopwatch.StartNew();
        FilterLog[] broadLogs = await web3.Eth.Filters.GetLogs.SendRequestAsync(broadFilter);
        broadStopwatch.Stop();

        NethereumEvmLogSource source = CreateLogSource(TimeSpan.FromSeconds(10));
        var filteredStopwatch = Stopwatch.StartNew();
        IReadOnlyList<RawEvmLog> filteredLogs = await source.GetTransferLogsAsync(
            ChainId,
            fromBlock,
            toBlock,
            [SenderAddress],
            CancellationToken.None);
        filteredStopwatch.Stop();

        Assert.Equal(irrelevantTransferCount + 1, broadLogs.Length);
        Assert.Single(filteredLogs);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            irrelevantTransferCount,
            broadReturnedLogs = broadLogs.Length,
            filteredReturnedLogs = filteredLogs.Count,
            broadElapsedMilliseconds = broadStopwatch.Elapsed.TotalMilliseconds,
            filteredElapsedMilliseconds = filteredStopwatch.Elapsed.TotalMilliseconds,
        }));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1_000)]
    [Trait("Category", "PerformanceProbe")]
    public async Task FilteredQueryRecordsBlockRangeScaling(int blockRange)
    {
        var web3 = new Web3(fixture.RpcAddress.ToString());
        await SetRuntimeCodeAsync(TokenAddress, BuildTransferEmitterRuntime());
        TransactionReceipt receipt = await EmitTransferAsync(web3, TokenAddress);
        long fromBlock = checked((long)receipt.BlockNumber.Value);
        await MineBlocksAsync(blockRange - 1);

        NethereumEvmLogSource source = CreateLogSource(TimeSpan.FromSeconds(10));
        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<RawEvmLog> logs = await source.GetTransferLogsAsync(
            ChainId,
            fromBlock,
            checked(fromBlock + blockRange - 1),
            [SenderAddress],
            CancellationToken.None);
        stopwatch.Stop();

        Assert.Single(logs);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            blockRange,
            elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            returnedLogs = logs.Count,
        }));
    }

    private async Task SetRuntimeCodeAsync(string contractAddress, string runtimeCode)
    {
        using var client = new HttpClient { BaseAddress = fixture.RpcAddress };
        using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                method = "anvil_setCode",
                @params = new[] { contractAddress, runtimeCode },
                id = 1,
            }),
        };
        using HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("error", out JsonElement error), error.ToString());
        Assert.True(body.RootElement.TryGetProperty("result", out _));
    }

    private async Task MineBlocksAsync(int blockCount)
    {
        if (blockCount <= 0)
        {
            return;
        }

        using var client = new HttpClient { BaseAddress = fixture.RpcAddress };
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            string.Empty,
            new
            {
                jsonrpc = "2.0",
                method = "anvil_mine",
                @params = new[] { $"0x{blockCount:x}" },
                id = 2,
            });
        response.EnsureSuccessStatusCode();
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("error", out JsonElement error), error.ToString());
    }

    private static async Task<TransactionReceipt> EmitTransferAsync(
        Web3 web3,
        string contractAddress)
    {
        string transactionHash = await web3.Eth.Transactions.SendTransaction.SendRequestAsync(
            new TransactionInput
            {
                From = SenderAddress,
                To = contractAddress,
                Gas = new HexBigInteger(250_000),
            });
        return await WaitForReceiptAsync(web3, transactionHash);
    }

    private NethereumEvmLogSource CreateLogSource(TimeSpan? requestTimeout = null) => new(
        Options.Create(new EvmIndexerOptions
        {
            RpcRequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(2),
            Networks =
            [
                new EvmNetworkOptions
                {
                    Name = "anvil",
                    ChainId = ChainId,
                    RpcUrl = fixture.RpcAddress.ToString(),
                    StartBlock = 0,
                    ConfirmationDepth = 0,
                },
            ],
        }));

    private static async Task<TransactionReceipt> WaitForReceiptAsync(
        Web3 web3,
        string transactionHash)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            TransactionReceipt? receipt = await web3.Eth.Transactions.GetTransactionReceipt
                .SendRequestAsync(transactionHash);
            if (receipt is not null)
            {
                return receipt;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        throw new TimeoutException($"Anvil did not mine transaction {transactionHash}.");
    }

    private static string BuildTransferEmitterRuntime(
        string fromAddress = SenderAddress,
        string toAddress = RecipientAddress,
        long rawAmount = 5_000_000)
    {
        string amount = rawAmount.ToString("x64", CultureInfo.InvariantCulture);
        string fromTopic = fromAddress[2..].PadLeft(64, '0');
        string toTopic = toAddress[2..].PadLeft(64, '0');
        string transferTopic = Erc20TransferLogDecoder.TransferTopic[2..];
        var code = new StringBuilder("0x");
        code.Append("7f").Append(amount).Append("600052");
        code.Append("7f").Append(toTopic);
        code.Append("7f").Append(fromTopic);
        code.Append("7f").Append(transferTopic);
        code.Append("60206000a300");
        return code.ToString();
    }

    private sealed class SingleEntryWatchlistProjection(
        string monitoredAddress,
        DateTimeOffset occurredAt) : IWatchlistProjectionStore
    {
        private readonly ProjectedWatchlistEntry entry = new(
            Guid.Parse("01991ef2-1234-7d20-83ef-53cd755f09c1"),
            ChainId,
            42,
            84,
            monitoredAddress,
            "Anvil treasury",
            occurredAt);

        public Task<IReadOnlyList<ProjectedWatchlistEntry>> FindMatchesAsync(
            long chainId,
            string fromAddress,
            string toAddress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool matches = chainId == entry.ChainId &&
                (string.Equals(entry.Address, fromAddress, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(entry.Address, toAddress, StringComparison.OrdinalIgnoreCase));
            IReadOnlyList<ProjectedWatchlistEntry> result = matches ? [entry] : [];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<ProjectedWatchlistEntry>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectedWatchlistEntry>>([entry]);

        public Task ApplyAddedAsync(
            Guid eventId,
            DateTimeOffset occurredAt,
            AddressAddedV1 integrationEvent,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ApplyUpdatedAsync(
            Guid eventId,
            DateTimeOffset occurredAt,
            AddressUpdatedV1 integrationEvent,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ApplyRemovedAsync(
            Guid eventId,
            DateTimeOffset occurredAt,
            AddressRemovedV1 integrationEvent,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ResetAsync(
            Guid eventId,
            DateTimeOffset occurredAt,
            WatchlistProjectionResetV1 integrationEvent,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CapturingNotificationStore : ITransferNotificationStore
    {
        public List<PreparedTransferNotification> Notifications { get; } = [];

        public Task<int> PersistAsync(
            TransferIntegrationEventEnvelope envelope,
            IReadOnlyCollection<PreparedTransferNotification> notifications,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Notifications.AddRange(notifications);
            return Task.FromResult(notifications.Count);
        }
    }

    private sealed class AnvilNetworkResolver : INetworkPresentationResolver
    {
        public NetworkPresentation Resolve(long chainId) =>
            new(chainId == ChainId ? "Anvil" : $"Chain {chainId}", null);
    }

    private sealed class InMemoryTelegramDeliveryStore : ITelegramDeliveryStore
    {
        public TelegramDeliveryState State { get; private set; } =
            new(Guid.Empty, TelegramDeliveryStatus.Pending, 0);

        public Task<TelegramDeliveryState> RegisterAsync(
            TelegramDeliveryRequestedV1 request,
            DateTimeOffset registeredAtUtc,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (State.EventId == Guid.Empty)
            {
                State = new TelegramDeliveryState(
                    request.EventId,
                    TelegramDeliveryStatus.Pending,
                    0);
            }

            return Task.FromResult(State);
        }

        public Task<int> BeginAttemptAsync(
            Guid eventId,
            DateTimeOffset attemptedAtUtc,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int attempt = State.AttemptCount + 1;
            State = State with { AttemptCount = attempt, HasIncompleteAttempt = true };
            return Task.FromResult(attempt);
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
            cancellationToken.ThrowIfCancellationRequested();
            State = State with
            {
                Status = outcome == TelegramDeliveryAttemptOutcome.Delivered
                    ? TelegramDeliveryStatus.Delivered
                    : TelegramDeliveryStatus.RetryScheduled,
                HasIncompleteAttempt = false,
            };
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingTelegramSender : ITelegramMessageSender
    {
        public List<CapturedTelegramMessage> Messages { get; } = [];

        public Task<TelegramSendResult> SendAsync(
            long chatId,
            string message,
            string parseMode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(new CapturedTelegramMessage(chatId, message, parseMode));
            return Task.FromResult(TelegramSendResult.Success(1));
        }
    }

    private sealed class NoOpRateLimiter : ITelegramDeliveryRateLimiter
    {
        public Task WaitAsync(long chatId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NoOpDelay : IDeliveryDelay
    {
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed record CapturedTelegramMessage(
        long ChatId,
        string Message,
        string ParseMode);
}
