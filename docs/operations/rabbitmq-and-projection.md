# RabbitMQ delivery and Watchlist projection

Phase 6 uses at-least-once event delivery. Watchlist and EVM Indexer commit business state and an outbox row in one database transaction. A background dispatcher publishes the row and only then records `ProcessedAtUtc`. A crash between those operations can publish the same `EventId` more than once, so consumers must remain idempotent.

## Envelope

Every integration envelope contains:

- `eventId`;
- `eventType`;
- `eventVersion`;
- `occurredAt`;
- `correlationId`;
- optional `causationId`;
- `payload`.

Watchlist, transfer, and notification categories use separate broker message contracts so a queue subscribes only to the category it owns.

## Topology and failures

MassTransit declares category exchanges and the durable `notification-watchlist-projection` and `notification-transfer-matcher` queues with their bindings. Each consumer has one concurrent writer to preserve replay order and deterministic duplicate handling. Short transient failures use the configured retry intervals while the message remains unacknowledged.

After retries are exhausted, MassTransit moves the message to its queue-specific `_error` queue with exception details in headers. Messages with no matching consumer go to the corresponding `_skipped` queue. Operators should inspect and replay error messages only after correcting the cause.

Producer outboxes have an independent bounded retry policy. Rows that exhaust it remain in the producer database with `DeadLetteredAtUtc`, `DeliveryAttempts`, and `LastError`; they are not deleted.

## Projection replay

Call:

```text
POST /api/watchlist/events/replay
X-Correlation-ID: <optional-guid>
```

The Watchlist service publishes one idempotent reset event and then all retained address-added, address-updated, and address-removed events in original order. The Notification transaction clears its projection and Watchlist inbox rows, preserves transfer inbox rows, records the reset event, and then consumes the history normally. Repeating the same replay produces the same final projection.

The current MVP retains outbox history indefinitely. If pruning is introduced later, it must first add snapshots or another replay source.
