# EVMTracker

EVMTracker is a master's thesis project that monitors user-selected EVM addresses for ERC-20 transfers and reports matching activity through Telegram. The target implementation uses independently deployable .NET 10 services, PostgreSQL, RabbitMQ, and event-driven integration.

The repository is currently in Phase 1: solution and architecture foundation. See [Roadmap.md](Roadmap.md) for the complete implementation and research plan.

## Prerequisites

- .NET SDK 10.0.400 or a compatible 10.0 patch selected by `global.json`.
- PostgreSQL for running the Watchlist API against persistent storage.

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
