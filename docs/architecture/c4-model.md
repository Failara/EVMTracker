# C4 Architecture Model

These diagrams describe the implemented EVMTracker architecture. PostgreSQL cylinders represent service-owned databases; no service reads another service's data directly.

## System context

```mermaid
C4Context
    title EVMTracker system context
    Person(user, "Telegram user", "Manages monitored addresses and receives alerts")
    System(evmtracker, "EVMTracker", ".NET 10 microservices that monitor ERC-20 transfers")
    System_Ext(telegram, "Telegram Bot API", "Receives commands and delivers messages")
    System_Ext(evm, "EVM JSON-RPC provider", "Supplies confirmed blocks, logs, and token metadata")
    System_Ext(operator, "Operations platform", "Prometheus, Grafana, Tempo, and logs")

    Rel(user, telegram, "Sends commands and reads alerts")
    Rel(telegram, evmtracker, "Updates and messages", "HTTPS")
    Rel(evmtracker, telegram, "Sends alerts", "HTTPS")
    Rel(evmtracker, evm, "Reads chain state and ERC-20 logs", "JSON-RPC/HTTPS")
    Rel(evmtracker, operator, "Exports health, metrics, traces, and logs")
```

## Container view

```mermaid
C4Container
    title EVMTracker containers
    Person(user, "Telegram user")
    System_Ext(telegram, "Telegram Bot API")
    System_Ext(evm, "EVM JSON-RPC provider")

    System_Boundary(system, "EVMTracker") {
        Container(gateway, "Telegram Gateway", ".NET 10 worker", "Receives commands, calls Watchlist, and performs rate-limited delivery")
        Container(watchlist, "Watchlist API", "ASP.NET Core", "Owns monitored-address lifecycle and event replay")
        Container(indexer, "EVM Indexer", ".NET 10 worker", "Maintains a watchlist projection and retrieves confirmed filtered logs")
        Container(notification, "Notification", ".NET 10 worker", "Matches transfers and creates Telegram delivery requests")
        Container(broker, "RabbitMQ", "Message broker", "Durable versioned integration-event delivery")
        ContainerDb(watchlistDb, "Watchlist DB", "PostgreSQL", "Tracked addresses and outbox/event history")
        ContainerDb(indexerDb, "Indexer DB", "PostgreSQL", "Projection, checkpoints, transfers, canonical hashes, and outbox")
        ContainerDb(notificationDb, "Notification DB", "PostgreSQL", "Projection, inbox, notifications, orphan state, and outbox")
        ContainerDb(gatewayDb, "Gateway DB", "PostgreSQL", "Telegram request, attempt, and terminal delivery ledger")
        Container(observability, "Observability", "OTel Collector, Prometheus, Tempo, Grafana", "Metrics, traces, dashboards, and alerts")
    }

    Rel(user, telegram, "Uses")
    Rel(telegram, gateway, "Updates")
    Rel(gateway, telegram, "Messages")
    Rel(gateway, watchlist, "Manages subscriptions", "HTTP/JSON")
    Rel(watchlist, watchlistDb, "Reads/writes")
    Rel(watchlist, broker, "Publishes watchlist events")
    Rel(broker, indexer, "Delivers watchlist events")
    Rel(indexer, evm, "Queries head, logs, hashes, metadata", "JSON-RPC")
    Rel(indexer, indexerDb, "Reads/writes")
    Rel(indexer, broker, "Publishes observed/orphaned transfers")
    Rel(broker, notification, "Delivers watchlist and transfer events")
    Rel(notification, notificationDb, "Reads/writes")
    Rel(notification, broker, "Publishes delivery requests")
    Rel(broker, gateway, "Delivers notification requests")
    Rel(gateway, gatewayDb, "Reads/writes")
    Rel(watchlist, observability, "Telemetry")
    Rel(indexer, observability, "Telemetry")
    Rel(notification, observability, "Telemetry")
    Rel(gateway, observability, "Telemetry")
```

## EVM Indexer component view

```mermaid
C4Component
    title EVM Indexer components
    Container_Ext(broker, "RabbitMQ", "Event broker")
    System_Ext(evm, "EVM JSON-RPC provider", "Chain data")
    ContainerDb_Ext(db, "Indexer DB", "PostgreSQL", "Indexer-owned state")

    Container_Boundary(indexer, "EVM Indexer") {
        Component(projection, "Watchlist projection consumer", "MassTransit", "Applies address lifecycle events idempotently")
        Component(barrier, "Projection bootstrap barrier", "Hosted service", "Requests replay and prevents unsafe checkpoint advancement")
        Component(worker, "Network indexing worker", "Hosted service", "Selects confirmed ranges, catches up, and adapts range size")
        Component(source, "Filtered log source", "Nethereum", "Issues indexed from/to topic queries")
        Component(decoder, "Transfer decoder", "Application component", "Validates and normalizes ERC-20 logs")
        Component(metadata, "Token metadata resolver", "Nethereum/cache", "Resolves symbol, name, and decimals with fallbacks")
        Component(store, "Atomic indexer store", "EF Core", "Commits checkpoints, dedupe identities, hashes, and outbox rows")
        Component(reorg, "Reorganization coordinator", "Application component", "Finds a common ancestor and emits deterministic orphan events")
        Component(outbox, "Outbox dispatcher", "Hosted service", "Publishes retained events with bounded retries")
        Component(retention, "Retention worker", "Hosted service", "Bounds historical operational state")
    }

    Rel(broker, projection, "Watchlist lifecycle events")
    Rel(projection, db, "Updates local projection")
    Rel(barrier, broker, "Requests replay")
    Rel(barrier, db, "Checks projection readiness")
    Rel(worker, db, "Loads checkpoint and tracked topics")
    Rel(worker, source, "Requests a confirmed range")
    Rel(source, evm, "eth_blockNumber / eth_getLogs / eth_getBlockByNumber")
    Rel(source, decoder, "Raw logs")
    Rel(decoder, metadata, "Valid transfer")
    Rel(metadata, evm, "eth_call")
    Rel(worker, store, "Commits normalized observations")
    Rel(worker, reorg, "Verifies canonical history")
    Rel(reorg, store, "Rewinds and records orphan events")
    Rel(store, db, "Transaction")
    Rel(outbox, db, "Claims outbox rows")
    Rel(outbox, broker, "Observed/orphaned transfer events")
    Rel(retention, db, "Deletes expired operational history")
```

## Evidence and maintenance

- Boundary rationale: [`docs/adr/0001-microservice-boundaries.md`](../adr/0001-microservice-boundaries.md).
- Data ownership: [`docs/adr/0002-postgresql-and-data-ownership.md`](../adr/0002-postgresql-and-data-ownership.md).
- Event delivery: [`docs/adr/0003-rabbitmq-event-delivery.md`](../adr/0003-rabbitmq-event-delivery.md).
- Acquisition strategy: [`docs/adr/0004-evm-log-acquisition.md`](../adr/0004-evm-log-acquisition.md).
- HTTP and event contracts: [`docs/contracts/`](../contracts/).

Update this file whenever a deployable boundary, owned data store, or cross-service protocol changes.
