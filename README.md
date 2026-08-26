# EVMTracker

EVMTracker is a master's thesis project that monitors user-selected EVM addresses for ERC-20 transfers and reports matching activity through Telegram. The target implementation uses independently deployable .NET 10 services, PostgreSQL, RabbitMQ, and event-driven integration.

Phases 1 through 5 are implemented: the solution foundation, durable Watchlist API, Telegram command gateway, and confirmed ERC-20 log indexer. See [Roadmap.md](Roadmap.md) for the complete implementation and research plan.

## Prerequisites

- .NET SDK 10.0.400 or a compatible 10.0 patch selected by `global.json`.
- PostgreSQL for running the Watchlist API against persistent storage.
- A Telegram bot token created through BotFather for running the Telegram Gateway.
- At least one EVM JSON-RPC HTTP endpoint for running the indexer.

## Build and test

```powershell
dotnet restore EVMTracker.sln --configfile NuGet.Config
dotnet build EVMTracker.sln --configuration Release --no-restore
dotnet test EVMTracker.sln --configuration Release --no-build
```

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

## Telegram command gateway

Store the Telegram token in user secrets for local development. Never add it to `appsettings.json`:

```powershell
dotnet user-secrets set "TelegramBot:BotToken" "<bot-token>" --project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Worker
```

The gateway uses long polling and calls the Watchlist API through the `WatchlistApi:BaseUrl` setting, which defaults to `http://localhost:5150`. The request timeout and supported network names/chain IDs are configured in the gateway's `appsettings.json`.

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

For each configured network, the worker subtracts the confirmation depth from the chain head, requests bounded `Transfer(address,address,uint256)` log ranges, decodes indexed participants, resolves cached token metadata, and atomically stores processed-transfer identity, the next checkpoint, and a versioned outbox event. Provider range-limit or timeout responses reduce the next block-range size.

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
- `/` for basic service identity.

## Architecture rules

- Domain projects have no project dependencies.
- Application projects may depend on their own Domain and versioned contracts.
- Infrastructure projects may depend on their own Application and technical building blocks.
- Hosts compose their own Application and Infrastructure adapters.
- A service cannot reference another service's implementation projects.
- Services never read another service's database.

These rules are enforced by `EVMTracker.ArchitectureTests`.
