# Telegram Delivery Operations

Phase 8 completes the user-visible path from a confirmed ERC-20 log to a Telegram alert. Notification owns message composition; Telegram Gateway owns rate limiting, external API calls, retry policy, and delivery history.

## Delivery Flow

1. Notification persists a logical notification and a `notifications.telegram-delivery-requested.v1` outbox row in one transaction.
2. Notification's outbox dispatcher publishes a `NotificationIntegrationEventEnvelope` to RabbitMQ.
3. Telegram Gateway consumes the envelope from the durable `telegram-delivery` queue.
4. The gateway registers the request in its PostgreSQL delivery ledger.
5. A shared limiter reserves both a global permit and a per-chat permit.
6. The gateway records an attempt, sends the HTML message, and records its outcome.
7. A successful request is acknowledged. A terminal exception is rejected by the consumer and MassTransit moves it to `telegram-delivery_error`.

Command replies use the same limiter as asynchronous notifications, so all outbound bot traffic contributes to the configured throughput.

## Message Safety and Verification

Notification messages use Telegram HTML. User labels, token metadata, addresses, amounts, and network names are HTML-encoded before interpolation. Explorer URLs are built from the chain-specific `NotificationNetworks:Networks:ExplorerBaseUrl` setting.

Every alert includes:

- incoming, outgoing, or self-transfer direction;
- exact human-readable amount and token symbol;
- network name and confirmed state;
- wallet label plus shortened monitored address;
- linked sender, recipient, token contract, and transaction;
- confirmed block number.

If a network has no explorer configuration, the renderer falls back to escaped plain text. Missing token metadata uses the indexer's `TOKEN-<suffix>` symbol and raw token value.

## Retry and Rate-Limit Policy

`TelegramDelivery` controls delivery behavior:

```json
{
  "TelegramDelivery": {
    "QueueName": "telegram-delivery",
    "PrefetchCount": 32,
    "ConcurrentMessageLimit": 8,
    "MaximumAttempts": 5,
    "InitialRetryDelay": "00:00:01",
    "MaximumRetryDelay": "00:01:00",
    "JitterFactor": 0.2,
    "GlobalMessagesPerSecond": 30,
    "PerChatMessagesPerSecond": 1
  }
}
```

Telegram `RetryAfter` values take precedence and are honored exactly. Other transient failures use exponential backoff with bounded deterministic jitter. Telegram API rejections without a retry signal are treated as permanent. Bounded retries execute inside one consumer delivery; concurrency allows later messages to continue while another request waits.

## Persistence and Idempotency

Telegram Gateway owns the `telegram_gateway` schema:

- `telegram_deliveries` stores request content, aggregate attempt count, Telegram message ID, last error, timestamps, and terminal status.
- `telegram_delivery_attempts` stores every started attempt, outcome, failure reason, and retry delay.

The delivery event ID is the primary idempotency key, and notification ID is also unique. Re-delivery of an already successful event is acknowledged without another Telegram API call. An attempt interrupted before completion is resumed under the same attempt number after restart.

Telegram does not expose an idempotency key for `sendMessage`. A process crash after Telegram accepts a message but before the database commit can therefore produce a duplicate after broker redelivery. This is the unavoidable external-side-effect window in an at-least-once design and should be reported explicitly in thesis experiments.

## Database Migration

Apply the Telegram Gateway migration before enabling the worker in a deployed environment:

```powershell
dotnet ef database update `
  --project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Infrastructure `
  --startup-project src/Services/TelegramGateway/EVMTracker.TelegramGateway.Infrastructure `
  --context TelegramGatewayDbContext
```

The Notification outbox dispatcher is now active. Deploy or start Telegram Gateway first so its durable queue exists before Notification publishes pending delivery requests.

## Failure Triage

- Inspect `telegram_gateway.telegram_deliveries` for current status and the last failure.
- Inspect `telegram_gateway.telegram_delivery_attempts` for the retry sequence and honored delays.
- Inspect RabbitMQ `telegram-delivery_error` for permanently failed or malformed requests.
- Correct configuration or user/chat problems before replaying a dead-lettered event.
- Keep the Telegram bot token only in Telegram Gateway secrets; never place it in Notification configuration.
