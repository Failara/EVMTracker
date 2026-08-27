# Configuration, security, and observability

Phase 10 makes every deployable service fail fast on invalid configuration, emit redacted structured logs, expose OpenTelemetry metrics and traces, and propagate trace and correlation context across HTTP and RabbitMQ boundaries.

## Secret sources

Committed `appsettings.json` files contain no working database password, RabbitMQ credential, Telegram token, or RPC credential. Supply them through .NET user secrets during local development or environment variables in deployed environments.

Initialize local secrets with values for your own environment:

```powershell
dotnet user-secrets set "ConnectionStrings:Watchlist" "<postgres-connection-string>" --project src/Services/Watchlist/EVMTracker.Watchlist.Api
dotnet user-secrets set "RabbitMq:Username" "<rabbitmq-user>" --project src/Services/Watchlist/EVMTracker.Watchlist.Api
dotnet user-secrets set "RabbitMq:Password" "<rabbitmq-password>" --project src/Services/Watchlist/EVMTracker.Watchlist.Api

dotnet user-secrets set "ConnectionStrings:EvmIndexer" "<postgres-connection-string>" --project src/Services/EvmIndexer/EVMTracker.EvmIndexer.Worker
dotnet user-secrets set "RabbitMq:Username" "<rabbitmq-user>" --project src/Services/EvmIndexer/EVMTracker.EvmIndexer.Worker
dotnet user-secrets set "RabbitMq:Password" "<rabbitmq-password>" --project src/Services/EvmIndexer/EVMTracker.EvmIndexer.Worker
dotnet user-secrets set "EvmIndexer:Networks:0:RpcUrl" "<https-rpc-url>" --project src/Services/EvmIndexer/EVMTracker.EvmIndexer.Worker

dotnet user-secrets set "ConnectionStrings:Notification" "<postgres-connection-string>" --project src/Services/Notification/EVMTracker.Notification.Worker
dotnet user-secrets set "RabbitMq:Username" "<rabbitmq-user>" --project src/Services/Notification/EVMTracker.Notification.Worker
dotnet user-secrets set "RabbitMq:Password" "<rabbitmq-password>" --project src/Services/Notification/EVMTracker.Notification.Worker

dotnet user-secrets set "ConnectionStrings:TelegramGateway" "<postgres-connection-string>" --project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Worker
dotnet user-secrets set "RabbitMq:Username" "<rabbitmq-user>" --project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Worker
dotnet user-secrets set "RabbitMq:Password" "<rabbitmq-password>" --project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Worker
dotnet user-secrets set "TelegramBot:BotToken" "<botfather-token>" --project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Worker
```

Container configuration uses the standard double-underscore mapping. Important variables include:

```text
ConnectionStrings__Watchlist
ConnectionStrings__EvmIndexer
ConnectionStrings__Notification
ConnectionStrings__TelegramGateway
RabbitMq__Host
RabbitMq__Username
RabbitMq__Password
EvmIndexer__Networks__0__RpcUrl
TelegramBot__BotToken
Observability__OtlpEndpoint
Observability__TraceSamplingRatio
```

Do not put real values in `.env.example`, command transcripts, screenshots, test fixtures, or issue descriptions. Rotate a credential immediately if it is exposed.

## Startup validation

Every host validates its domain-specific options at startup. Empty connection strings, broker credentials, the Telegram token, and RPC URLs are intentionally invalid. Observability configuration additionally requires:

- a sampling ratio between `0` and `1`;
- a Prometheus path beginning with `/`;
- an empty OTLP endpoint or an absolute HTTP/HTTPS endpoint.

This means a clean checkout builds and tests without secrets but a live service does not start until its required secrets are supplied.

## Logs and sensitive data

Console output is newline-delimited structured JSON. Each record contains timestamp, severity, category, event ID, structured properties, and active W3C trace/span IDs. Correlation scopes add `CorrelationId` and public integration `EventId` values.

The formatter redacts:

- Telegram user, chat, and update identifiers;
- Telegram bot tokens and bearer tokens;
- password, token, authorization, credential, and connection-string fields;
- URI user information and common credential-bearing query parameters.

Application telemetry never records Telegram message text, monitored addresses, RPC URLs, connection strings, or provider responses as span attributes. Public chain IDs, transaction event IDs, block numbers, and event types are safe diagnostic identifiers.

## Metrics

Each host exposes a Prometheus endpoint at `/metrics` by default. Restrict this endpoint to an internal network or authenticated monitoring proxy in production.

Custom metrics include:

- `evmtracker.indexer.chain.head`;
- `evmtracker.indexer.last.processed.block`;
- `evmtracker.indexer.chain.lag`;
- `evmtracker.indexer.rpc.duration` and `evmtracker.indexer.rpc.failures`;
- `evmtracker.indexer.logs.decoded` and `evmtracker.indexer.logs.malformed`;
- `evmtracker.indexer.reorganizations`;
- `evmtracker.notification.watchlist.matches` and `evmtracker.notification.created`;
- `evmtracker.messaging.outbox.published` and `evmtracker.messaging.outbox.failures`;
- `evmtracker.telegram.deliveries`, partitioned by outcome.

RabbitMQ queue depth comes from RabbitMQ's Prometheus plugin (`rabbitmq_queue_messages_ready`) because the broker, rather than an application process, owns authoritative queue state.

## Traces and correlation

ASP.NET Core and outgoing `HttpClient` calls are instrumented automatically. MassTransit transport spans and explicit publish/consume spans carry W3C context across RabbitMQ. Integration envelopes retain `CorrelationId` and `CausationId` through transactional outbox boundaries, where execution may continue in a new trace after a restart.

For an ERC-20 transfer, search by the observed transfer `EventId` or its stable `CorrelationId`. That identifier appears on the indexer outbox event, Notification consumer/outbox event, and Telegram delivery consumer, while the delivery event ID links to `telegram_gateway.telegram_deliveries` and its attempt records.

Set `Observability:OtlpEndpoint` to an OpenTelemetry Collector endpoint to export metrics and traces. Redacted JSON logs remain on standard output so a container log collector receives only the sanitized representation. Leave the endpoint empty to use Prometheus scraping without OTLP traffic.

## Dashboard and alerts

The repository provides:

- `deploy/observability/grafana/dashboards/evmtracker-overview.json`;
- `deploy/observability/prometheus/alerts.yml`.

The dashboard covers chain progress, lag, RPC latency/failures, decoded logs, watchlist matches, RabbitMQ backlog, and Telegram outcomes. Alerts detect stalled indexing, excessive RPC failures, broker backlog, outbox dead letters, and Telegram failures. Phase 11 will provision these assets through Docker Compose.
