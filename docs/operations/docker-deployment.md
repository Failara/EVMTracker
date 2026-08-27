# Docker deployment

Phase 11 packages the four .NET 10 hosts as non-root multi-stage images and supplies a complete Docker Compose environment for local development, testnet demonstrations, and repeatable thesis experiments.

## What the stack runs

`compose.yaml` starts:

- Watchlist API, EVM Indexer, Notification Worker, and Telegram Gateway;
- one PostgreSQL 18 container per service boundary;
- one-shot EF Core migration containers for all four databases;
- RabbitMQ with the management and Prometheus plugins;
- OpenTelemetry Collector and Grafana Tempo for traces;
- Prometheus and Grafana with the Phase 10 alerts and dashboard provisioned automatically.

Service processes run as the non-root user supplied by the official .NET 10 runtime image. PostgreSQL, RabbitMQ, Prometheus, Tempo, and Grafana data use named volumes and survive an ordinary `docker compose down`.

## Required local values

Create the untracked environment file:

```powershell
Copy-Item .env.example .env
```

Fill every empty password plus these two externally issued values:

- `TELEGRAM_BOT_TOKEN`: the token created through BotFather;
- `EVM_RPC_URL`: an HTTPS JSON-RPC endpoint for the configured EVM network.

Generate independent random passwords for the four databases, RabbitMQ, and Grafana. Do not reuse the Telegram token or RPC credential as an infrastructure password. `.env` is ignored by both Git and the Docker build context.

For Sepolia, also set `EVM_START_BLOCK` near the block range used for the demonstration. Leaving it at `0` requests a broad historical ERC-20 scan and can consume substantial provider quota.

## One-command startup

Ensure Docker Desktop or another Linux-container Docker Engine is running, then execute:

```powershell
docker compose up --build --detach
```

Compose waits for every PostgreSQL container and RabbitMQ to become healthy. Each migration container must then exit successfully before its application service starts. The Telegram Gateway additionally waits for the Watchlist API health check.

Inspect the result:

```powershell
docker compose ps --all
docker compose logs --follow --tail 100
```

Expected endpoints:

| Component | URL |
| --- | --- |
| Watchlist API | `http://localhost:5150/health/ready` |
| EVM Indexer | `http://localhost:5074/health/ready` |
| Notification Worker | `http://localhost:5212/health/ready` |
| Telegram Gateway | `http://localhost:5079/health/ready` |
| RabbitMQ management | `http://localhost:15672` |
| Prometheus | `http://localhost:9090` |
| Grafana | `http://localhost:3000` |
| Tempo API | `http://localhost:3200` |

Use the `RABBITMQ_USER`/`RABBITMQ_PASSWORD` and `GRAFANA_ADMIN_USER`/`GRAFANA_ADMIN_PASSWORD` values from your local `.env` when signing in.

## Migration workflow

Migrations run automatically during `docker compose up`. To apply them again without rebuilding application images:

```powershell
docker compose run --rm watchlist-migrations
docker compose run --rm indexer-migrations
docker compose run --rm notification-migrations
docker compose run --rm telegram-migrations
```

Each design-time context reads the same `ConnectionStrings__...` environment variable used by its service. Migration images contain the pinned local `dotnet-ef` tool and compiled migration assemblies. The commands are idempotent: EF Core applies only migrations not already present in the service-owned history table.

After adding a new migration, rebuild only the matching migration and application images:

```powershell
docker compose build watchlist-migrations watchlist-api
docker compose run --rm watchlist-migrations
docker compose up --detach watchlist-api
```

Replace the Watchlist service names with the corresponding Indexer, Notification, or Telegram names when migrating those databases.

## Configuration profiles

### Local development

Use Sepolia and a recent start block. Keep `TRACE_SAMPLING_RATIO=1.0` while diagnosing behavior. The Compose ports intentionally expose service health, RabbitMQ management, Prometheus, Grafana, and Tempo only to the Docker host's published interfaces; do not expose this configuration directly to the internet.

### Testnet experiments

Set `EVM_NETWORK_NAME`, `EVM_CHAIN_ID`, `EVM_RPC_URL`, `EVM_START_BLOCK`, and `EVM_CONFIRMATION_DEPTH` for the selected configured network. Ensure the Telegram Gateway's supported-network catalog contains the same network command name. Record provider limits, start block, confirmation depth, and experiment time in the thesis results.

### Demonstration

Use a dedicated Telegram bot and dedicated testnet RPC key. Select a recent start block before starting the indexer, add only demonstration addresses, and verify Grafana, RabbitMQ, and all `/health/ready` endpoints before transferring tokens. Rotate the bot token and provider key if they appear in a recording or screenshot.

Mainnet deployment requires a separate security and capacity review. This Compose file is a single-host MVP deployment, not a high-availability production topology.

## Observability provisioning

Applications export OTLP metrics and traces to the collector. The collector forwards traces to Tempo and exposes application metrics to Prometheus. Prometheus also scrapes RabbitMQ's authoritative queue metrics on port `15692`. Grafana provisions both data sources and the EVMTracker dashboard on first start.

Configuration files:

- `deploy/observability/otel-collector/config.yml`;
- `deploy/observability/tempo/tempo.yml`;
- `deploy/observability/prometheus/prometheus.yml`;
- `deploy/observability/prometheus/alerts.yml`;
- `deploy/observability/grafana/provisioning/`;
- `deploy/observability/grafana/dashboards/evmtracker-overview.json`.

## Recovery checks

Application containers use `restart: unless-stopped`; MassTransit and the background workers reconnect after transient dependency failures. Basic operator drills are:

```powershell
docker compose restart rabbitmq
docker compose restart watchlist-db indexer-db notification-db telegram-db
docker compose ps --all
```

Confirm that services return to healthy state, checkpoints continue advancing, queues drain, and no duplicate logical notification appears. Automated restart/failure drills belong to Phase 12; Phase 11 supplies the durable volumes, retries, health checks, and observability required to run them safely.

## Shutdown and data retention

Stop containers while retaining all data:

```powershell
docker compose down
```

`docker compose down --volumes` permanently removes the four databases, broker state, metrics, traces, and Grafana state. Use that command only when intentionally resetting the complete environment.
