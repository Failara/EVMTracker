# ADR 0001: Initial microservice boundaries

- Status: Accepted
- Date: 2026-08-26

## Context

The thesis must demonstrate microservice and DDD concepts while remaining feasible for one developer. Splitting every technical concern into a service would increase operational work without producing a useful domain boundary.

## Decision

Use four independently deployable services:

1. Watchlist owns monitored addresses and user-specific tracking settings.
2. EVM Indexer owns blockchain acquisition, normalization, checkpoints, and reorganization detection.
3. Notification owns watchlist projections, transfer matching, notification identity, and rendering.
4. Telegram Gateway owns Telegram input and output adapters, bot credentials, and API rate limiting.

Each service owns its persistence and exposes behavior only through documented HTTP APIs or integration events. The source is kept in a modular monorepo for consistent builds and manageable thesis development.

## Consequences

- Services can fail and scale independently along workload boundaries.
- Eventual consistency and integration-event compatibility become explicit design concerns.
- Local development requires several processes and infrastructure dependencies.
- New services require evidence that an existing boundary has become unsuitable.
