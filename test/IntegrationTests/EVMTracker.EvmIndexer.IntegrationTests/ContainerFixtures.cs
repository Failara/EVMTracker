using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace EVMTracker.EvmIndexer.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class ContainerInfrastructureDefinition :
    ICollectionFixture<PostgreSqlContainerFixture>,
    ICollectionFixture<RabbitMqContainerFixture>,
    ICollectionFixture<AnvilContainerFixture>
{
    public const string Name = "container-infrastructure";
}

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder(
            "postgres:18.6-alpine3.24")
        .WithDatabase("evmtracker_tests")
        .WithUsername("evmtracker_tests")
        .WithPassword("evmtracker_tests")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    public RabbitMqContainer Container { get; } = new RabbitMqBuilder(
            "rabbitmq:4.2.9-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public Uri Address => new(Container.GetConnectionString());

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

public sealed class AnvilContainerFixture : IAsyncLifetime
{
    private const ushort AnvilPort = 8545;

    public IContainer Container { get; } = new ContainerBuilder(
            "ghcr.io/foundry-rs/foundry:v1.7.1")
        .WithEntrypoint("anvil")
        .WithCommand(
            "--host",
            "0.0.0.0",
            "--port",
            AnvilPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--chain-id",
            "31337",
            "--accounts",
            "3")
        .WithPortBinding(AnvilPort, true)
        .WithWaitStrategy(
            Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(AnvilPort))
        .Build();

    public Uri RpcAddress => new(
        $"http://{Container.Hostname}:{Container.GetMappedPublicPort(AnvilPort)}");

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
