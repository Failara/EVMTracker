# ADR 0002: PostgreSQL and service-owned data

- Status: Accepted
- Date: 2026-08-26

## Context

Watchlists, EVM checkpoints, projections, and notifications require durable transactional state. Cross-service table access would couple schemas and deployments even if separate service processes were used.

## Decision

Use PostgreSQL for durable service data. Each service owns a separate database or database schema and database credentials. Services must not query or mutate another service's tables. Cross-boundary data is transferred through APIs and versioned events.

State changes that must publish an event use a transactional outbox. Consumers use stable identities and inbox/deduplication records where at-least-once delivery can repeat messages.

## Consequences

- Local transactions remain simple and reliable within a service boundary.
- Cross-service queries require APIs or local projections.
- Schema migrations can be deployed per service.
- Eventual consistency must be visible in user flows and operational metrics.
