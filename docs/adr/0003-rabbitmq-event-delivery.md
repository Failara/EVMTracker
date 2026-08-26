# ADR 0003: RabbitMQ and at-least-once delivery

- Status: Accepted
- Date: 2026-08-26

## Context

Blockchain ingestion, watchlist changes, matching, and Telegram delivery have different throughput and failure modes. Synchronous chaining would propagate provider or Telegram outages through the complete monitoring path.

## Decision

Use RabbitMQ for durable integration events and Telegram delivery requests. Treat delivery as at least once. Use versioned contracts, explicit consumer queues, bounded retry with jitter, and dead-letter queues for messages that cannot be processed.

Consumers must be idempotent. A broker acknowledgement is sent only after the consumer's durable state change commits.

## Consequences

- Temporary downstream failures create observable backlog instead of losing activity.
- Duplicate delivery is expected and tested.
- Message contracts require compatibility governance.
- RabbitMQ becomes an operational dependency with queue-depth and dead-letter monitoring.
