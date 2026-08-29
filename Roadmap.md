# EVMTracker Project Roadmap

This document describes the current state of the master's thesis project and the work required to build an MVP Telegram bot for monitoring user-added EVM addresses through ERC-20 event logs.

The short version: Phases 1 through 13 are implemented. The repository has a buildable .NET 10 microservice foundation, durable Watchlist management through Telegram, a watchlist-filtered confirmed-block ERC-20 indexer, reliable rate-limited Telegram delivery, bounded EVM reorganization recovery, bounded operational storage, production-oriented OpenTelemetry diagnostics, a reproducible Docker Compose deployment, container-backed integration and recovery tests, and a reproducible thesis research package.

**Thesis topic:** Models for organizing monitoring of distributed ledgers (EVM) based on microservice architecture.

**Status:** Phase 13 implementation is complete. The hardening update is live-verified on BSC mainnet and Telegram; disposable PostgreSQL, RabbitMQ, and Anvil Testcontainers automate recovery and deterministic transfer scenarios; and the repository now contains predeclared hypotheses, experiment manifests and runners, a raw smoke baseline, C4/OpenAPI/AsyncAPI documentation, an architectural comparison, and a thesis chapter draft. Publication-grade repeated measurements and university-specific editing remain final academic work rather than a software phase.

## Current State

### Implemented

The repository currently contains:

- `Roadmap.md` with the MVP scope, proposed architecture, project structure, implementation phases, testing strategy, and thesis evaluation plan.
- A .NET 10 solution containing four independently deployable service hosts and Clean Architecture layer projects.
- Central build and package configuration with nullable reference types, .NET analyzers, formatting rules, and warnings treated as errors.
- Shared liveness and readiness endpoints used by every service host.
- Four service unit-test projects and an architecture-test project that enforces dependency boundaries.
- A CI workflow for restore, formatting verification, Release build, and tests.
- Accepted ADRs for service boundaries, PostgreSQL data ownership, RabbitMQ delivery, and EVM log acquisition.
- A Watchlist domain model containing EIP-55 `Address`, `ChainId`, `TrackedAddress`, domain errors/results, and duplicate-subscription policy.
- Watchlist domain tests covering address checksums, aggregate ownership, labels, networks, removal, and duplicate tracking rules.
- MediatR commands, queries, and handlers for adding, listing, updating, and removing tracked addresses.
- EF Core PostgreSQL persistence with an initial migration and a unique user/address/network constraint.
- A transactional outbox that stores versioned Watchlist lifecycle events in the same database transaction as aggregate changes.
- Watchlist REST endpoints for add, list, update, and remove operations.
- Watchlist application and persistence integration tests, including duplicate enforcement and aggregate/outbox atomicity.
- Telegram long polling with cancellation and bounded retry delay.
- Telegram command parsing and routing for `/start`, `/help`, `/add`, `/list`, `/remove`, and `/status`.
- A typed Watchlist HTTP client with timeouts, correlation IDs, error mapping, and network-specific removal behavior.
- Startup validation for the Telegram token, Watchlist API URL, polling settings, and supported network catalog.
- Telegram command and HTTP adapter tests that run without Telegram, PostgreSQL, or a live Watchlist service.
- Configurable multi-network EVM polling with confirmation-depth safe heads and bounded block ranges.
- An event-driven Indexer Watchlist projection that limits `eth_getLogs` to tracked `from` and `to` topics instead of ingesting the complete network transfer firehose.
- Automatic Watchlist event replay when the Indexer projection is empty, plus a startup barrier that prevents checkpoints advancing before subscriptions are available.
- Checkpoint-based downtime recovery that processes missed ranges without an artificial polling delay until the confirmed chain head is reached.
- Nethereum-backed `eth_blockNumber`, `eth_getLogs`, and ERC-20 metadata calls.
- ERC-20 `Transfer` decoding from indexed `from`/`to` topics with normalized transfer identity.
- Durable per-network checkpoints, processed-transfer deduplication, and transactional transfer outbox recording.
- Adaptive block-range reduction for provider timeouts and range-limit responses.
- Deterministic indexer unit and SQLite persistence integration tests.
- Versioned event envelopes with event, correlation, and causation metadata.
- Watchlist, observed-transfer, orphaned-transfer, projection-reset, and Telegram-delivery contracts.
- MassTransit 8.5.10 with RabbitMQ category exchanges and durable consumer queues.
- Watchlist and EVM Indexer outbox dispatch with bounded retry and retained failure state.
- An idempotent Notification Watchlist projection with an atomic inbox and service-owned PostgreSQL schema.
- Event-stream replay through the Watchlist API without cross-service database reads.
- Contract compatibility and Notification projection integration tests.
- A durable transfer-matcher queue that compares both ERC-20 participants with the local Watchlist projection.
- Incoming, outgoing, and self-transfer classification for every matching subscription.
- Exact raw-token conversion using ERC-20 decimals without floating-point arithmetic.
- Stable logical notification identities and a database uniqueness constraint for duplicate defense.
- Atomic notification, delivery-outbox, and transfer-inbox persistence.
- Configurable network display names with a safe chain-ID fallback.
- Escaped, explorer-linked Telegram HTML alerts with transfer direction, wallet identity, token details, block number, and confirmation state.
- A durable Telegram delivery consumer queue and an active Notification outbox dispatcher.
- Configurable global and per-chat outbound rate limiting shared by alerts and command replies.
- Telegram `RetryAfter` handling and bounded exponential retry with jitter for transient failures.
- A Telegram Gateway-owned PostgreSQL delivery ledger with request, attempt, error, Telegram message ID, and terminal-status history.
- Event-ID idempotency for successful delivery redelivery and durable RabbitMQ error queues for terminal failures.
- Configurable per-network canonical block-hash retention and pre-checkpoint chain verification.
- Common-ancestor discovery, atomic checkpoint rewind, and durable orphaned-transfer state.
- Block-specific observation event IDs with stable logical transfer and notification identities.
- Idempotent orphan tombstones that suppress late stale observations.
- Pending-alert cancellation, published-alert correction, and canonical reappearance handling.
- Validated observability options and user-secrets support for every deployable host.
- Redacting structured JSON logs containing W3C trace/span and stable correlation identifiers.
- OpenTelemetry instrumentation for ASP.NET Core, HTTP clients, MassTransit, EVM RPC calls, indexer progress, notification matching, outbox publication, and Telegram delivery.
- Prometheus endpoints, an EVMTracker Grafana dashboard, and alert rules for lag, RPC failures, queue backlog, outbox dead letters, and Telegram failures.
- Multi-stage, non-root .NET 10 images for all four deployable services and compact EF Core migration bundles.
- A Docker Compose stack containing service-owned PostgreSQL databases, RabbitMQ, OpenTelemetry Collector, Tempo, Prometheus, and Grafana.
- Health-based startup ordering, persistent volumes, automatically provisioned observability, environment templates, and deployment/recovery documentation.
- Live BSC hardening for transient JSON-RPC retries, invalid on-chain token metadata, and runtime-typed transfer-event serialization.
- Credential-backed validation in which a live 10-block BSC batch persisted and delivered 2,228 ERC-20 events through the Notification inbox with empty outboxes and no RabbitMQ faults.
- A successful RabbitMQ/PostgreSQL recovery drill and idempotent reruns of all four migration images.
- Periodic retention of completed outbox records, processed tracked transfers, projection inbox records, notification history, and orphan history.
- Rotated container logs, 5% default trace sampling, six-hour trace retention, and capped Prometheus retention.
- A BSC capacity diagnosis proving that unfiltered ingestion could not keep pace and accumulated hundreds of thousands of irrelevant rows; the broad scan was replaced by address-topic filtering.
- A post-downtime BSC recovery validation in which the clean Indexer restored one tracked address, resumed from block `118616200`, indexed transaction `0x7e214b9a983c5ec48c277a4830bcc1ed1d49b85ad033a7947ab85843d3882edf` at block `118616273`, and delivered its outgoing 5 USDT notification to Telegram on the first attempt.
- A post-catch-up storage validation in which each service-owned PostgreSQL database remained approximately 8 MB, the Indexer retained only eight relevant transfers and a 64-block canonical window, and Docker build cache was fully purged after image creation.
- Pinned Testcontainers fixtures for real PostgreSQL, RabbitMQ, and Foundry Anvil dependencies that remain isolated from the live Compose stack.
- Automated PostgreSQL and RabbitMQ restart drills, durable backlog recovery, poison-message dead-lettering, a real Anvil ERC-20 log-to-fake-Telegram scenario, RPC outage recovery, and diagnostic scale probes for 1/100/500 tracked addresses and 100/1,000 queued messages.
- A Phase 13 research package containing predeclared questions and hypotheses, a repeatable measurement protocol, machine-readable experiment definitions and baseline results, a randomized repeated-run script, C4 diagrams, OpenAPI and AsyncAPI contracts, a microservice/modular-monolith comparison, and a chapter-ready thesis draft.

### Not Yet Implemented

The implemented software roadmap is complete. Before academic submission, the project still needs:

- Publication-grade repeated measurements on the final unchanged revision, including external-provider validation where permitted.
- Statistical tables and figures generated from those final raw runs.
- Citations, university formatting, language editing, and the final defense presentation.

## Target MVP Behavior

The MVP should support this flow:

1. A Telegram user starts the bot.
2. The user sends `/add <network> <0x-address> [label]`.
3. The application validates and normalizes the EVM address.
4. The watchlist service saves the monitored address in its database.
5. A watchlist event updates the notification service's local address projection.
6. The same event updates the indexer's local filter projection.
7. The EVM indexer retrieves confirmed ERC-20 `Transfer` logs whose indexed `from` or `to` topic matches a tracked address.
8. The indexer decodes the indexed `from` and `to` topics and publishes a normalized transfer event.
9. The notification service checks both transfer participants against active watchlists.
10. A matching transfer produces a Telegram notification request.
11. The Telegram gateway sends the alert while respecting Telegram rate limits and retry rules.

The notification should contain:

- wallet label and monitored address;
- transfer direction: incoming or outgoing;
- network and token symbol;
- human-readable token amount;
- token contract address;
- transaction hash and block number;
- explorer links;
- confirmation state.

## Target Microservice Architecture

The MVP should use four independently deployable services. This is enough to demonstrate DDD, asynchronous communication, service-owned data, eventual consistency, and failure isolation without creating unnecessary services.

### `Watchlist.Service`

Responsible for:

- Telegram user identity references.
- Monitored EVM addresses.
- Address labels and enabled networks.
- Enabling, disabling, listing, and removing watchlist entries.
- Publishing watchlist lifecycle events.

Owns its PostgreSQL database. No other service may read its tables directly.

### `EvmIndexer.Worker`

Responsible for:

- Reading chain heads and confirmed block ranges.
- Calling `eth_getLogs` for the ERC-20 `Transfer` event.
- Maintaining an event-driven local projection of active filter addresses.
- Catching up from its persisted checkpoint after planned or unplanned downtime.
- Decoding and normalizing transfer logs.
- Maintaining a durable checkpoint for every network.
- Detecting chain reorganizations and rewinding safely.
- Publishing normalized ERC-20 transfer events.

Owns indexer checkpoints, its Watchlist filter projection, processed log identities, recent block hashes, and token metadata cache data. Old published history is removed by configured retention policies.

### `Notification.Service`

Responsible for:

- Maintaining a local projection of active watchlist addresses.
- Matching ERC-20 `from` and `to` addresses.
- Applying notification rules.
- Rendering Telegram notification content.
- Preventing duplicate logical notifications.
- Publishing Telegram delivery requests.

Owns its watchlist projection, notification records, deduplication keys, and delivery state.

### `TelegramGateway.Worker`

Responsible for:

- Receiving `/start`, `/help`, `/add`, `/list`, `/remove`, and `/status` commands.
- Converting user commands into watchlist API requests.
- Returning friendly validation and application errors.
- Consuming delivery requests and sending Telegram messages.
- Applying Telegram rate limits and retrying transient failures.

The Telegram bot token belongs only to this service.

### Supporting Infrastructure

- PostgreSQL for service-owned persistent data.
- RabbitMQ for durable asynchronous events and delivery requests.
- OpenTelemetry for correlated logs, metrics, and traces.
- Prometheus and Grafana for monitoring.
- Docker Compose for local development and demonstration.
- A local EVM node such as Anvil for deterministic integration tests.

## Proposed Project Structure

The project should use a modular monorepo. Each business service keeps its own Clean Architecture projects and can be built and deployed independently.

```text
EVMTracker/
├── EVMTracker.sln
├── Roadmap.md
├── README.md
├── Directory.Build.props
├── Directory.Packages.props
├── .editorconfig
├── .env.example
├── .gitignore
├── compose.yaml
├── src/
│   ├── BuildingBlocks/
│   │   ├── EVMTracker.Contracts/
│   │   │   ├── Watchlists/
│   │   │   ├── Transfers/
│   │   │   └── Notifications/
│   │   ├── EVMTracker.Messaging/
│   │   └── EVMTracker.Observability/
│   └── Services/
│       ├── Watchlist/
│       │   ├── EVMTracker.Watchlist.Domain/
│       │   ├── EVMTracker.Watchlist.Application/
│       │   ├── EVMTracker.Watchlist.Infrastructure/
│       │   └── EVMTracker.Watchlist.Api/
│       ├── EvmIndexer/
│       │   ├── EVMTracker.EvmIndexer.Domain/
│       │   ├── EVMTracker.EvmIndexer.Application/
│       │   ├── EVMTracker.EvmIndexer.Infrastructure/
│       │   └── EVMTracker.EvmIndexer.Worker/
│       ├── Notification/
│       │   ├── EVMTracker.Notification.Domain/
│       │   ├── EVMTracker.Notification.Application/
│       │   ├── EVMTracker.Notification.Infrastructure/
│       │   └── EVMTracker.Notification.Worker/
│       └── TelegramGateway/
│           ├── EVMTracker.TelegramGateway.Application/
│           ├── EVMTracker.TelegramGateway.Infrastructure/
│           └── EVMTracker.TelegramGateway.Worker/
├── test/
│   ├── UnitTests/
│   ├── IntegrationTests/
│   ├── ContractTests/
│   ├── EndToEndTests/
│   ├── ResilienceTests/
│   └── PerformanceTests/
├── docs/
│   ├── architecture/
│   ├── adr/
│   ├── api/
│   ├── asyncapi/
│   ├── operations/
│   └── thesis/
│       ├── research-plan.md
│       ├── experiments.md
│       └── results/
├── deploy/
│   ├── monitoring/
│   └── kubernetes/
└── scripts/
```

### Internal Service Layers

Each business service should follow the same dependency direction:

- `Domain` contains aggregates, entities, value objects, domain rules, and domain events. It has no infrastructure dependencies.
- `Application` contains commands, queries, handlers, ports, and use-case orchestration.
- `Infrastructure` contains EF Core, RabbitMQ, EVM JSON-RPC, Telegram, and other external adapters.
- `Api` or `Worker` is the composition root and hosts HTTP endpoints, message consumers, or background services.

`BuildingBlocks` may contain technical primitives and versioned integration contracts. Domain entities and business rules must not be shared between services.

## Recommended Technology Stack

- Current .NET LTS and ASP.NET Core.
- Entity Framework Core with PostgreSQL.
- Nethereum for EVM JSON-RPC and ABI/log decoding.
- MassTransit with RabbitMQ.
- `Telegram.Bot` for Telegram integration.
- MediatR for commands and queries inside service boundaries.
- FluentValidation for request validation.
- OpenAPI for synchronous service APIs.
- AsyncAPI or JSON Schema for integration events.
- xUnit, Testcontainers, and Anvil for automated tests.
- OpenTelemetry, Serilog, Prometheus, and Grafana for observability.

Exact library choices should be recorded in Architecture Decision Records before implementation.

## Recommended Implementation Order

## Phase 1: Create the Solution Foundation

Goal: create a buildable service skeleton before adding business behavior.

Tasks:

- [x] Create `EVMTracker.sln`.
- [x] Scaffold the four services and building-block projects.
- [x] Add central package and build configuration.
- [x] Add formatting, analyzers, nullable reference types, and warnings-as-errors rules.
- [x] Add initial unit-test projects.
- [x] Add health and readiness endpoints to each host.
- [x] Add CI for restore, build, test, and formatting validation.
- [x] Write the first ADRs for service boundaries, PostgreSQL, RabbitMQ, and the EVM acquisition model.

Recommended tests:

- [x] Every service builds independently.
- [x] Dependency tests prevent `Domain` from referencing `Infrastructure`.
- [x] Every service exposes a healthy startup state with valid configuration.

## Phase 2: Build the Watchlist Domain Model

Goal: represent the rules for monitoring user-owned EVM addresses.

Tasks:

- [x] Add an `Address` value object with hexadecimal validation and EIP-55 normalization.
- [x] Add a `ChainId` value object.
- [x] Add the `TrackedAddress` aggregate root.
- [x] Store the Telegram user/chat reference, label, enabled networks, status, and creation time.
- [x] Prevent one user from tracking the same address twice on the same network.
- [x] Add methods to rename, enable, disable, and remove a tracked address.
- [x] Add `Result` and `Result<T>` primitives for expected business failures.

Design notes:

- Telegram command parsing does not belong in the aggregate.
- The database is the source of truth.
- Addresses should be compared by normalized 20-byte value, not display casing.
- Private keys and transaction signing are outside the project scope.

Recommended tests:

- [x] Valid lowercase and checksummed addresses normalize to one value.
- [x] Malformed or incorrectly sized addresses fail.
- [x] Duplicate watchlist entries fail.
- [x] A user cannot change or remove another user's entry.
- [x] Enabling or disabling networks preserves aggregate invariants.

## Phase 3: Implement Watchlist Persistence and Use Cases

Status: implemented and verified.

Goal: make watchlist management durable and accessible to other adapters.

Tasks:

- [x] Add `ITrackedAddressRepository` to the application layer.
- [x] Add MediatR commands and queries:
  - `AddTrackedAddressCommand`
  - `RemoveTrackedAddressCommand`
  - `ListTrackedAddressesQuery`
  - `UpdateTrackedAddressCommand`
- [x] Add EF Core mappings and migrations.
- [x] Add a database-level unique constraint for user, chain, and normalized address.
- [x] Add REST endpoints to the Watchlist API.
- [x] Add a transactional outbox for watchlist lifecycle events.
- [x] Record versioned `AddressAdded`, `AddressUpdated`, and `AddressRemoved` integration events in the outbox. RabbitMQ dispatch is implemented in Phase 6.

Recommended tests:

- [x] Repository can save and load a tracked address.
- [x] Duplicate records are rejected consistently.
- [x] List returns only the requesting user's records.
- [x] Database change and outbox event are committed atomically.

## Phase 4: Implement Telegram Commands

Status: implemented and verified.

Goal: let real users manage their watchlists through Telegram.

Tasks:

- [x] Add Telegram update receiving using polling for local development.
- [x] Add command routing for:
  - `/start`
  - `/help`
  - `/add <network> <address> [label]`
  - `/list`
  - `/remove <network> <address>`
  - `/status`
- [x] Convert Telegram input into Watchlist API requests.
- [x] Return friendly responses for malformed addresses, duplicates, unsupported networks, and unavailable services.
- [x] Add request timeouts, cancellation, and correlation IDs.
- [x] Keep webhook support as a deployment enhancement after the polling MVP works.

Recommended tests:

- [x] `/add` parses valid lowercase, uppercase, and checksummed input.
- [x] Missing parameters produce usage guidance.
- [x] `/list` contains only the current user's entries.
- [x] Watchlist application errors map to user-friendly messages.

## Phase 5: Implement the EVM Indexer

Status: implemented and verified.

Goal: retrieve ERC-20 transfer activity without losing confirmed logs during restarts.

Tasks:

- [x] Add per-network RPC configuration and provider abstraction.
- [x] Read the latest chain head with `eth_blockNumber`.
- [x] Calculate a safe head using configurable confirmation depth.
- [x] Retrieve bounded block ranges through `eth_getLogs`.
- [x] Filter by the ERC-20 event signature:

  ```solidity
  event Transfer(address indexed from, address indexed to, uint256 value);
  ```

- [x] Decode `from` from `topic1`, `to` from `topic2`, and `value` from log data.
- [x] Normalize chain ID, contract address, participant addresses, transaction hash, block number, block hash, log index, and raw value.
- [x] Retrieve and cache token `symbol`, `name`, and `decimals` with safe fallbacks.
- [x] Store durable checkpoints and processed log identities.
- [x] Adapt block-range size when the RPC provider times out or rejects large ranges.
- [x] Record versioned `Erc20TransferObserved` events through a transactional outbox. RabbitMQ dispatch is implemented in Phase 6.

Important design note:

Wallet addresses are stored in the indexed log topics. The log's `address` field is the token contract, not the sender or receiver wallet.

Recommended tests:

- [x] A real ERC-20 Transfer log is decoded correctly.
- [x] Restart resumes from the last committed checkpoint.
- [x] Reprocessing the same range does not create a second logical event.
- [x] Missing or malformed token metadata uses a safe fallback.
- [x] A provider range-limit response reduces the next query size.

## Phase 6: Add Event Contracts and Watchlist Projection

Status: implemented and verified.

Goal: connect independently owned services without sharing databases.

Tasks:

- [x] Add versioned integration-event envelopes with:
  - `EventId`
  - `EventType`
  - `EventVersion`
  - `OccurredAt`
  - `CorrelationId`
  - `CausationId`
- [x] Define contracts for watchlist changes, observed transfers, orphaned transfers, and Telegram delivery requests.
- [x] Configure RabbitMQ exchanges, durable queues, bounded retry middleware, and error/dead-letter queues.
- [x] Build the notification service's local watchlist projection from watchlist events.
- [x] Make projection consumers idempotent.
- [x] Add replay support so a projection can be rebuilt without reading the Watchlist database.

Design note:

The projection endpoint uses short in-process retry intervals while the delivery remains unacknowledged. Once retries are exhausted, MassTransit moves the poisoned message to the durable `<queue>_error` queue; skipped messages use `<queue>_skipped`. This avoids requiring the RabbitMQ delayed-message plugin for the MVP. Longer scheduled redelivery can be added during resilience hardening if experiments show that it is needed.

Recommended tests:

- [x] Contract compatibility tests reject breaking changes.
- [x] Duplicate events do not duplicate projected addresses.
- [x] Replaying events produces the same final projection.
- [x] A poisoned event exhausts bounded retries and moves to the broker error queue without blocking later messages.

## Phase 7: Match ERC-20 Transfers

Status: implemented and verified.

Goal: create one logical notification for every relevant transfer and subscription.

Tasks:

- [x] Compare both normalized `from` and `to` addresses with the local watchlist projection.
- [x] Classify matches as incoming, outgoing, or self-transfer.
- [x] Support multiple users monitoring the same address.
- [x] Resolve the matching label and network display information.
- [x] Convert the raw token value using token decimals.
- [x] Create a stable notification identity from subscription ID, chain ID, transaction hash, and log index.
- [x] Persist the notification before publishing a Telegram delivery request.
- [x] Do not require a USD price feed for the initial ERC-20 monitoring MVP.

Design note:

Notification persistence uses two idempotency levels. The transfer inbox rejects repeated broker delivery by source `EventId`, while the unique logical identity rejects an equivalent transfer published under another event ID. Notification state, the Telegram delivery-request outbox row, and the inbox marker commit in one database transaction.

The Notification outbox dispatcher was activated in Phase 8 after Telegram Gateway declared its durable delivery queue. This preserves the deployment invariant that a delivery request is not published before a consumer queue can retain it.

Recommended tests:

- [x] A match occurs when the monitored address appears in `topic1`.
- [x] A match occurs when the monitored address appears in `topic2`.
- [x] An unrelated Transfer log is ignored.
- [x] Multiple subscriptions produce separate user notifications.
- [x] Duplicate broker delivery does not create duplicate logical notifications.

## Phase 8: Render and Dispatch Telegram Notifications

Status: implemented and verified.

Goal: send compact, verifiable alerts without overwhelming Telegram.

Tasks:

- [x] Render wallet label, direction, network, token amount, token contract, and confirmation state.
- [x] Add transaction and address explorer links from network configuration.
- [x] Escape Telegram formatting correctly.
- [x] Add configurable global and per-chat rate limiters.
- [x] Respect Telegram `RetryAfter` responses.
- [x] Retry transient errors with bounded exponential backoff and jitter.
- [x] Move permanently failed delivery requests to a dead-letter queue.
- [x] Persist delivery attempts and final status.

Recommended tests:

- [x] Incoming and outgoing notifications render correctly.
- [x] Unknown token metadata still produces a useful message.
- [x] Markdown or HTML special characters are escaped.
- [x] Rate limits do not exceed configured throughput.
- [x] Transient Telegram failures do not stop the consumer.

## Phase 9: Add Idempotency and Reorganization Handling

Status: implemented and verified.

Goal: remain correct under at-least-once delivery, service restarts, and EVM chain reorganizations.

Tasks:

- [x] Identify a transfer by `chainId + transactionHash + logIndex`.
- [x] Identify a notification by `subscriptionId + chainId + transactionHash + logIndex`.
- [x] Add inbox tables or equivalent consumer deduplication where needed.
- [x] Store recent canonical block hashes inside a configurable reorganization window.
- [x] Compare stored block hashes before advancing the checkpoint.
- [x] On mismatch, find the common ancestor and rewind the indexer.
- [x] Mark orphaned transfers and publish `TransferOrphaned` events.
- [x] Suppress unconfirmed notifications according to confirmation depth.
- [x] Send a correction only if a previously notified transfer becomes orphaned.

Recommended tests:

- [x] Broker redelivery does not duplicate domain effects.
- [x] Indexer restart does not miss confirmed transfers.
- [x] A simulated short reorganization rewinds to the correct ancestor.
- [x] Orphaned transfers are removed from active notification state.

## Phase 10: Configuration, Security, and Observability

Status: implemented and verified.

Goal: make live behavior diagnosable and configuration safe.

Tasks:

- [x] Add strongly typed options with startup validation.
- [x] Configure secrets through user secrets locally and environment variables in containers.
- [x] Never commit Telegram tokens, RPC credentials, or database passwords.
- [x] Add structured logging with trace and correlation IDs.
- [x] Redact Telegram user data and provider credentials from logs.
- [x] Add metrics for:
  - current chain head;
  - last processed block;
  - chain lag;
  - RPC request duration and failures;
  - logs decoded;
  - watchlist matches;
  - broker queue depth;
  - notifications sent and failed.
- [x] Add distributed tracing across HTTP requests and broker messages.
- [x] Create Grafana dashboards and basic alerts.

Recommended tests:

- [x] Missing required settings fail fast with a useful error.
- [x] Sensitive values do not appear in logs.
- [x] A transfer can be traced from EVM log identity to Telegram delivery status.

## Phase 11: Dockerize and Deploy the MVP

Status: implemented and live-verified with Docker Desktop, a real Telegram bot, and BSC mainnet.

Goal: run the complete system predictably from a clean checkout.

Tasks:

- [x] Add one Dockerfile per deployable service.
- [x] Add `.dockerignore`.
- [x] Add `compose.yaml` for:
  - Watchlist API;
  - EVM Indexer Worker;
  - Notification Worker;
  - Telegram Gateway Worker;
  - PostgreSQL databases;
  - RabbitMQ;
  - Prometheus and Grafana.
- [x] Add health checks and startup dependency handling.
- [x] Document database migration commands.
- [x] Document local, testnet, and demonstration configuration.
- [ ] Add optional Kubernetes manifests only after the Compose stack passes live recovery validation.

Recommended tests:

- [x] A clean environment starts with one documented command.
- [x] Services recover after RabbitMQ and PostgreSQL restarts.
- [x] Migrations can be applied without rebuilding application images.
- [x] A clean Indexer database restores the Watchlist projection, catches up missed confirmed blocks, and delivers the recovered transfer through Telegram.

## Phase 12: Integration, Resilience, and Performance Testing

Status: implemented and verified.

Goal: validate failures and service interactions that unit tests cannot cover.

Tasks:

- [x] Use Testcontainers for PostgreSQL and RabbitMQ integration tests.
- [x] Use Anvil and a deterministic ERC-20 `Transfer` emitter for chain scenarios.
- [x] Add an end-to-end test from token transfer to a fake Telegram adapter.
- [x] Add failure drills for:
  - RPC outage;
  - broker restart;
  - database restart;
  - service crash/offline consumer;
  - duplicate event;
  - poisoned event;
  - Telegram throttling;
  - chain reorganization.
- [x] Add performance probes for increasing event rates and watchlist sizes.
- [x] Record recovery time, duplicate rate, missed-event rate, and process working-set change in structured test output.

Recommended tests:

- [x] No confirmed transfers are missed during controlled restart tests.
- [x] No duplicate logical notifications appear during redelivery tests.
- [x] Backlogged events are processed after dependency recovery.
- [x] The harness emits repeatable throughput measurements without machine-dependent pass/fail thresholds.

## Phase 13: Thesis Experiments and Documentation

Status: implemented as a reproducible research framework with a committed smoke baseline. Final repeated measurements must be collected immediately before thesis submission.

Goal: evaluate architectural models rather than only demonstrate a working bot.

Experiments:

1. Compare broad ERC-20 Transfer-log scanning with dynamically filtered `from` and `to` topic queries as watchlist size grows.
2. Compare block-range sizes and indexer concurrency by RPC volume, throughput, latency, and provider failures.
3. Measure notification latency at different confirmation depths.
4. Measure backlog recovery with different consumer counts.
5. Inject duplicate events, dependency outages, restarts, and reorganizations and measure correctness and recovery time.
6. Compare the microservice solution with a modular-monolith baseline or analytical model by coupling, deployability, fault isolation, resource use, and operational complexity.

Tasks:

- [x] Define hypotheses before running experiments.
- [x] Store benchmark scripts with the repository.
- [x] Record hardware, software, network, and provider conditions.
- [x] Save the raw smoke baseline under `docs/thesis/results/`.
- [x] Separate measured conclusions from architectural reasoning.
- [x] Document threats to validity and experiment limitations.
- [x] Complete C4 diagrams, ADRs, OpenAPI, AsyncAPI, runbooks, and a thesis chapter draft.
- [ ] Replace the smoke baseline with at least five measured repetitions per case on the final submission revision.
- [ ] Add final statistical tables, figures, academic citations, university formatting, and defense slides.

## Suggested Next Concrete Step

All software phases are implemented. The next concrete step is the final academic evidence pass:

1. Freeze the revision and run `./scripts/run-thesis-experiments.ps1` without changing the implementation between cases.
2. Repeat the external EVM validation with documented, credential-redacted provider conditions.
3. Calculate the required distribution statistics and generate thesis-ready tables and figures from every retained TRX run.
4. Add academic citations, apply university formatting, and prepare the defense presentation.

## MVP Completion Checklist

### Planning and Architecture

- [x] Define project purpose and MVP scope.
- [x] Define target microservice boundaries.
- [x] Describe the proposed repository structure.
- [x] Define implementation phases and thesis experiments.
- [x] Create C4 context, container, and component diagrams.
- [x] Write initial Architecture Decision Records.

### Watchlist Management

- [x] `Address` and `ChainId` value objects.
- [x] `TrackedAddress` aggregate.
- [x] Domain result/error primitives and duplicate-subscription policy.
- [x] EF Core database context and migrations.
- [x] Watchlist commands and queries.
- [x] Watchlist REST API.
- [x] Transactional watchlist outbox.
- [x] Telegram `/add`, `/list`, `/remove`, and `/status` commands.

### EVM Monitoring

- [x] Multi-network RPC configuration.
- [x] Confirmed block-range polling.
- [x] ERC-20 Transfer log retrieval and decoding.
- [x] Durable per-network checkpoints.
- [x] Token metadata resolution with safe fallbacks.
- [x] Processed-transfer identity and transactional outbox recording.
- [x] Reorganization detection and rewind.
- [x] Idempotent transfer publication.

### Notifications

- [x] Watchlist projection in the Notification service.
- [x] Incoming, outgoing, and self-transfer address matching.
- [x] Notification persistence and deduplication.
- [x] Explorer-linked Telegram rendering.
- [x] Global and per-chat rate limiting.
- [x] Retry and dead-letter handling.
- [x] Orphaned-transfer correction behavior.

### Platform and Quality

- [x] RabbitMQ integration.
- [x] PostgreSQL database ownership per service.
- [x] Redacted structured logs plus OpenTelemetry metrics and traces.
- [x] Health and readiness checks.
- [x] Dockerfiles and Docker Compose.
- [x] CI build and test workflow.
- [x] Architecture boundary tests.
- [x] Unit-test project scaffolds.
- [x] Watchlist domain unit tests.
- [x] Watchlist application handler tests.
- [x] Watchlist persistence integration tests.
- [x] Telegram command and Watchlist HTTP adapter tests.
- [x] EVM indexer domain and pipeline tests.
- [x] EVM indexer persistence integration tests.
- [x] Unit tests for remaining MVP business behavior.
- [x] Event-contract compatibility tests.
- [x] Notification Watchlist-projection integration tests.
- [x] Integration and contract tests for the remaining service behavior.
- [x] End-to-end test with Anvil.
- [x] Resilience and performance tests.
- [x] Deployment and operations documentation.

### Thesis Completion

- [x] Research hypotheses finalized.
- [x] Monitoring-model smoke comparison completed.
- [x] Microservice and modular-monolith analytical comparison completed.
- [x] Raw smoke benchmark results committed.
- [x] Baseline results analyzed with threats to validity.
- [ ] Thesis chapters and presentation completed.
- [x] End-to-end MVP validated on a live EVM network.

## MVP Definition of Done

The MVP is complete when:

- a Telegram user can add, list, and remove monitored EVM addresses;
- confirmed ERC-20 transfers involving those addresses produce Telegram notifications;
- each service owns its data and communicates through documented APIs or events;
- restarts and duplicate broker deliveries do not create missed or duplicate logical notifications;
- a controlled chain reorganization is handled correctly;
- the complete platform starts from a clean checkout using documented commands;
- dashboards expose chain lag, broker backlog, matches, and delivery failures;
- experiments are reproducible and provide evidence for the thesis conclusions.
