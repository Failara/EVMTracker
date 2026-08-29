using MassTransit;

namespace EVMTracker.EvmIndexer.IntegrationTests;

[Collection(ContainerInfrastructureDefinition.Name)]
public sealed class RabbitMqRecoveryTests(RabbitMqContainerFixture fixture)
{
    [Fact]
    [Trait("Category", "ContainerIntegration")]
    public async Task DurableBacklogSurvivesBrokerRestart()
    {
        string queueName = $"phase12-recovery-{Guid.NewGuid():N}";
        Guid eventId = Guid.NewGuid();
        IBusControl topologyBus = CreateConsumerBus(queueName, _ => Task.CompletedTask);
        await topologyBus.StartAsync();
        await topologyBus.StopAsync();

        IBusControl publisher = CreatePublisherBus();
        await publisher.StartAsync();
        await publisher.Publish(new BrokerProbe(eventId));
        await publisher.StopAsync();

        await fixture.Container.StopAsync();
        await fixture.Container.StartAsync();

        var received = new TaskCompletionSource<BrokerProbe>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IBusControl recoveryBus = CreateConsumerBus(
            queueName,
            context =>
            {
                received.TrySetResult(context.Message);
                return Task.CompletedTask;
            });

        await recoveryBus.StartAsync();
        try
        {
            BrokerProbe message = await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(eventId, message.EventId);
        }
        finally
        {
            await recoveryBus.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "ContainerIntegration")]
    public async Task PoisonedMessageMovesToDurableErrorQueueAfterRetries()
    {
        string queueName = $"phase12-poison-{Guid.NewGuid():N}";
        int attempts = 0;
        var retriesExhausted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IBusControl failingBus = Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host(fixture.Address);
            configuration.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.UseMessageRetry(retry => retry.Immediate(2));
                endpoint.Handler<BrokerProbe>(_ =>
                {
                    if (Interlocked.Increment(ref attempts) >= 3)
                    {
                        retriesExhausted.TrySetResult();
                    }

                    throw new InvalidOperationException("Deliberate poison-message failure.");
                });
            });
        });

        await failingBus.StartAsync();
        try
        {
            await failingBus.Publish(new BrokerProbe(Guid.NewGuid()));
            await retriesExhausted.Task.WaitAsync(TimeSpan.FromSeconds(30));

            var moved = new TaskCompletionSource<BrokerProbe>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            IBusControl errorQueueBus = CreateConsumerBus(
                $"{queueName}_error",
                context =>
                {
                    moved.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            await errorQueueBus.StartAsync();
            try
            {
                await moved.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal(3, Volatile.Read(ref attempts));
            }
            finally
            {
                await errorQueueBus.StopAsync();
            }
        }
        finally
        {
            await failingBus.StopAsync();
        }
    }

    private IBusControl CreatePublisherBus() =>
        Bus.Factory.CreateUsingRabbitMq(configuration =>
            configuration.Host(fixture.Address));

    private IBusControl CreateConsumerBus(
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

public sealed record BrokerProbe(Guid EventId);
