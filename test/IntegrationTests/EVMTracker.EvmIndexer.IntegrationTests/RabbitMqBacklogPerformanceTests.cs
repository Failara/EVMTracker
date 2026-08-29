using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using MassTransit;
using Xunit.Abstractions;

namespace EVMTracker.EvmIndexer.IntegrationTests;

[Collection(ContainerInfrastructureDefinition.Name)]
public sealed class RabbitMqBacklogPerformanceTests(
    RabbitMqContainerFixture fixture,
    ITestOutputHelper output)
{
    [Theory]
    [InlineData(100)]
    [InlineData(1_000)]
    [Trait("Category", "PerformanceProbe")]
    public async Task OfflineConsumerDrainsBacklogWithoutMissesOrDuplicates(int messageCount)
    {
        string queueName = $"phase12-backlog-{messageCount}-{Guid.NewGuid():N}";
        IBusControl topologyBus = CreateBus(queueName, _ => Task.CompletedTask);
        await topologyBus.StartAsync();
        await topologyBus.StopAsync();

        Guid[] eventIds = Enumerable.Range(0, messageCount)
            .Select(_ => Guid.NewGuid())
            .ToArray();
        IBusControl publisher = Bus.Factory.CreateUsingRabbitMq(configuration =>
            configuration.Host(fixture.Address));
        await publisher.StartAsync();
        await Task.WhenAll(eventIds.Select(eventId =>
            publisher.Publish(new BrokerProbe(eventId))));
        await publisher.StopAsync();

        var unique = new ConcurrentDictionary<Guid, byte>();
        int deliveries = 0;
        var drained = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IBusControl consumer = CreateBus(
            queueName,
            context =>
            {
                unique.TryAdd(context.Message.EventId, 0);
                if (Interlocked.Increment(ref deliveries) == messageCount)
                {
                    drained.TrySetResult();
                }

                return Task.CompletedTask;
            });
        long memoryBefore = Process.GetCurrentProcess().WorkingSet64;
        var stopwatch = Stopwatch.StartNew();
        await consumer.StartAsync();
        try
        {
            await drained.Task.WaitAsync(TimeSpan.FromSeconds(60));
        }
        finally
        {
            stopwatch.Stop();
            await consumer.StopAsync();
        }

        long memoryAfter = Process.GetCurrentProcess().WorkingSet64;
        int duplicates = deliveries - unique.Count;
        int missed = messageCount - unique.Count;
        double messagesPerSecond = messageCount / stopwatch.Elapsed.TotalSeconds;
        output.WriteLine(JsonSerializer.Serialize(new
        {
            messageCount,
            elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            messagesPerSecond,
            duplicates,
            missed,
            processWorkingSetDeltaBytes = memoryAfter - memoryBefore,
        }));

        Assert.Equal(messageCount, unique.Count);
        Assert.Equal(0, duplicates);
        Assert.Equal(0, missed);
    }

    private IBusControl CreateBus(
        string queueName,
        Func<ConsumeContext<BrokerProbe>, Task> handler) =>
        Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host(fixture.Address);
            configuration.ReceiveEndpoint(
                queueName,
                endpoint => endpoint.Handler<BrokerProbe>(context => handler(context)));
        });
}
