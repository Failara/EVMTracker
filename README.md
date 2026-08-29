# EVMTracker

EVMTracker is a master's thesis project that monitors user-selected EVM addresses for ERC-20 transfers and reports matching activity through Telegram. The target implementation uses independently deployable .NET 10 services, PostgreSQL, RabbitMQ, and event-driven integration.

Phases 1 through 13 are implemented: the solution foundation, durable Watchlist API, Telegram command gateway, confirmed ERC-20 log indexer, RabbitMQ outbox delivery, durable transfer matching, reliable Telegram alerts, bounded chain-reorganization recovery, OpenTelemetry-based operational diagnostics, a reproducible Docker Compose deployment, container-backed integration/recovery tests, and a reproducible thesis experiment package. See [Roadmap.md](Roadmap.md) for the complete implementation plan and [the thesis research package](docs/thesis/README.md) for hypotheses, methodology, architecture comparison, raw baseline data, and the chapter draft.

## Prerequisites

- .NET SDK 10.0.400 or a compatible 10.0 patch selected by `global.json`.
- PostgreSQL for service-owned Watchlist, Indexer, Notification, and Telegram Gateway storage.
- RabbitMQ for asynchronous integration events.
- A Telegram bot token created through BotFather for running the Telegram Gateway.
- At least one EVM JSON-RPC HTTP endpoint for running the indexer.

For the containerized stack, install Docker Desktop or another Linux-container Docker Engine with Docker Compose.

## Docker Compose deployment

Copy the environment template, fill its empty secrets locally, and start the complete stack:

```powershell
Copy-Item .env.example .env
docker compose up --build --detach
```

The untracked `.env` file must contain independent database, RabbitMQ, and Grafana passwords plus a real `TELEGRAM_BOT_TOKEN` and `EVM_RPC_URL`. Set `EVM_START_BLOCK` near the intended testnet range before the first run to avoid an unnecessarily broad historical scan.

The deployment runs the four services, four service-owned PostgreSQL databases, one-shot EF Core migrations, RabbitMQ, OpenTelemetry Collector, Tempo, Prometheus, and Grafana. See [docker-deployment.md](docs/operations/docker-deployment.md) for endpoints, migration commands, testnet/demo profiles, recovery checks, and data-retention behavior.

## Build and test

```powershell
dotnet restore EVMTracker.sln --configfile NuGet.Config
dotnet build EVMTracker.sln --configuration Release --no-restore
dotnet test EVMTracker.sln --configuration Release --no-build
```

The full test run includes isolated PostgreSQL, RabbitMQ, and Anvil Testcontainers and therefore requires Docker. Use `./scripts/run-phase12-tests.ps1` to run only the container-backed recovery and performance scenarios. See [phase12-testing.md](docs/operations/phase12-testing.md) for coverage and test-output guidance.

Use `./scripts/run-thesis-experiments.ps1` for the Phase 13 protocol. It records a non-secret environment manifest, performs a warm-up, randomizes the experiment-case order, and writes each measured repetition as a TRX artifact. The committed one-run baseline validates the harness but is not a substitute for repeated thesis measurements.

## Watchlist database

Configure `ConnectionStrings__Watchlist` through an environment variable or user secrets. The checked-in connection string contains no password.

Restore the local EF tool and apply migrations with:

```powershell
dotnet tool restore --configfile NuGet.Config
dotnet tool run dotnet-ef database update --project src/Services/Watchlist/EVMTracker.Watchlist.Infrastructure --startup-project src/Services/Watchlist/EVMTracker.Watchlist.Infrastructure --context WatchlistDbContext
```

The Watchlist API exposes:

- `POST /api/watchlist`
- `GET /api/watchlist/users/{telegramUserId}`
- `PUT /api/watchlist/{trackedAddressId}`
- `DELETE /api/watchlist/{trackedAddressId}?telegramUserId={telegramUserId}`
- `POST /api/watchlist/events/replay`

## Telegram command gateway

Store the Telegram token in user secrets for local development. Never add it to `appsettings.json`:

```powershell
dotnet user-secrets set "TelegramBot:BotToken" "<bot-token>" --project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Worker
```

The gateway uses long polling and calls the Watchlist API through the `WatchlistApi:BaseUrl` setting, which defaults to `http://localhost:5150`. The request timeout and supported network names/chain IDs are configured in the gateway's `appsettings.json`.

Configure its delivery ledger and apply the migration before starting the gateway:

```powershell
$env:ConnectionStrings__TelegramGateway = "Host=localhost;Port=5432;Database=evmtracker_telegram_gateway;Username=postgres;Password=<password>"
dotnet tool run dotnet-ef database update --project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Infrastructure --startup-project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Infrastructure --context TelegramGatewayDbContext
```

Start the Watchlist API and Telegram Gateway in separate terminals:

```powershell
dotnet run --project src/Services/Watchlist/EVMTracker.Watchlist.Api
dotnet run --project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Worker
```

Supported bot commands:

- `/start`
- `/help`
- `/add <network> <0x-address> [label]`
- `/list`
- `/remove <network> <0x-address>`
- `/status`

The gateway validates configuration at startup, attaches an `X-Correlation-ID` header to Watchlist requests, applies HTTP timeouts and cancellation, and maps Watchlist failures to user-friendly Telegram replies.

## EVM indexer

Configure the indexer's PostgreSQL connection and RPC URL through environment variables or another secret provider. The checked-in settings contain neither a database password nor an RPC endpoint:

```powershell
$env:ConnectionStrings__EvmIndexer = "Host=localhost;Port=5432;Database=evmtracker_indexer;Username=postgres;Password=<password>"
$env:EvmIndexer__Networks__0__RpcUrl = "https://<provider-endpoint>"
```

Set `EvmIndexer:Networks:0:StartBlock` close to the intended demonstration range before the first run. Starting at block zero performs a broad historical ERC-20 scan and may require substantial provider capacity and time.

Apply the indexer migration with:

```powershell
dotnet tool run dotnet-ef database update --project src/Services/EvmIndexer/EVMTracker.EvmIndexer.Infrastructure --startup-project src/Services/EvmIndexer/EVMTracker.EvmIndexer.Infrastructure --context IndexerDbContext
```

Then start the worker:

```powershell
dotnet run --project src/Services/EvmIndexer/EVMTracker.EvmIndexer.Worker
```

For each configured network, the worker subtracts the confirmation depth from the chain head and requests bounded `Transfer(address,address,uint256)` log ranges filtered by tracked addresses in the indexed `from` and `to` topics. It decodes matching participants, resolves cached token metadata, and atomically stores processed-transfer identity, the next checkpoint, and a versioned outbox event. Provider range-limit or timeout responses reduce the next block-range size.

The indexer maintains its own event-driven Watchlist projection. An empty projection pauses checkpoint advancement and triggers a Watchlist replay request. After downtime, the worker resumes from the persisted next block and processes missed ranges without the normal polling delay until it catches the confirmed head. Published rows and old tracked-transfer history are removed by configurable retention workers.

The worker also retains `ReorganizationWindow` canonical hashes. Before advancing a checkpoint it verifies the latest retained hash, rewinds to a discovered common ancestor on mismatch, and publishes deterministic orphan events. See `docs/operations/reorganization-handling.md` for the recovery model and operator procedure.

## RabbitMQ and Notification projection

Watchlist and EVM Indexer outbox workers publish category-specific versioned envelopes through RabbitMQ. Configure the broker with `RabbitMq__Host`, `RabbitMq__Port`, `RabbitMq__VirtualHost`, `RabbitMq__Username`, and `RabbitMq__Password`. Checked-in broker credentials are intentionally empty and startup validation rejects them.

MassTransit is pinned to the open-source `8.5.10` line. The Notification service consumes Watchlist envelopes from the durable `notification-watchlist-projection` queue, retries transient failures at configured intervals, and leaves exhausted poison messages in `notification-watchlist-projection_error`. Projection updates and inbox deduplication commit in one database transaction.

Apply the Notification migration with:

```powershell
dotnet tool run dotnet-ef database update --project src/Services/Notification/EVMTracker.Notification.Infrastructure --startup-project src/Services/Notification/EVMTracker.Notification.Infrastructure --context NotificationDbContext
```

Then start RabbitMQ and the Notification worker before generating Watchlist events:

```powershell
$env:ConnectionStrings__Notification = "Host=localhost;Port=5432;Database=evmtracker_notification;Username=postgres;Password=<password>"
dotnet run --project src/Services/Notification/EVMTracker.Notification.Worker
```

To rebuild the Notification projection exclusively from retained integration events, call `POST /api/watchlist/events/replay`. The Watchlist service publishes a reset marker followed by its ordered lifecycle-event history; the Notification service never reads the Watchlist database.

## ERC-20 transfer matching

The Notification worker consumes confirmed transfer envelopes from `notification-transfer-matcher`. It queries only its local Watchlist projection and compares both normalized participants:

- monitored recipient: incoming;
- monitored sender: outgoing;
- monitored sender and recipient: self-transfer.

Each matching subscription creates its own notification, so several users may monitor the same address independently. Raw token values are converted with integer arithmetic and the event's token decimals; missing decimals leave the raw value intact.

The logical notification ID is deterministic over subscription ID, chain ID, transaction hash, and log index. A matching database uniqueness constraint provides a second duplicate-defense layer beyond the consumed-event inbox. The notification, `TelegramDeliveryRequestedV1` outbox row, and inbox marker commit atomically.

The Notification outbox dispatcher and Telegram Gateway's durable `telegram-delivery` queue are enabled. Delivery attempts and terminal status are stored in the gateway database; see `docs/operations/telegram-delivery.md` for configuration and failure handling.

Network display names are configured under `NotificationNetworks:Networks`. Unknown positive chain IDs use an `EVM chain <chainId>` fallback rather than rejecting an otherwise valid transfer.

Orphan events mark matching notifications inactive. Pending alerts are cancelled, while already published alert requests produce one escaped explorer-linked correction. Orphan tombstones prevent late stale observation events from recreating invalid notifications.

## Service hosts

| Host | Purpose |
| --- | --- |
| `EVMTracker.Watchlist.Api` | Owns monitored-address management |
| `EVMTracker.EvmIndexer.Worker` | Retrieves and normalizes ERC-20 logs |
| `EVMTracker.Notification.Worker` | Matches transfers and prepares notifications |
| `EVMTracker.TelegramGateway.Worker` | Receives bot commands and sends messages |

Every host exposes:

- `/health/live` for process liveness;
- `/health/ready` for service readiness;
- `/metrics` for Prometheus scraping;
- `/` for basic service identity.

## Configuration and observability

All four hosts support .NET user secrets locally and environment-variable configuration in deployed environments. Their checked-in configuration contains no usable database, broker, RPC, or Telegram credentials. Empty required values fail during startup with a setting-specific validation error.

Console logs are structured JSON and redact Telegram identifiers, tokens, connection credentials, URI user information, and common secret query parameters. Active trace/span IDs and stable correlation IDs connect HTTP, RabbitMQ, indexer, Notification, and Telegram activity without recording message text or monitored addresses.

OpenTelemetry exports traces and metrics through an optional `Observability__OtlpEndpoint`; redacted JSON logs remain on standard output, and Prometheus scraping remains available when OTLP is disabled. The supplied Grafana dashboard and Prometheus alerts cover chain progress and lag, RPC behavior, decoded logs, matches, broker backlog, outbox failures, and Telegram outcomes. See [configuration-and-observability.md](docs/operations/configuration-and-observability.md) for secret setup, metric names, trace lookup, and security guidance.

## Architecture rules

- Domain projects have no project dependencies.
- Application projects may depend on their own Domain and versioned contracts.
- Infrastructure projects may depend on their own Application and technical building blocks.
- Hosts compose their own Application and Infrastructure adapters.
- A service cannot reference another service's implementation projects.
- Services never read another service's database.

These rules are enforced by `EVMTracker.ArchitectureTests`.
