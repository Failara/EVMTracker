# EVMTracker Project Roadmap

This document describes the current state of the master's thesis project and the work required to build an MVP Telegram bot for monitoring user-added EVM addresses through ERC-20 event logs.

The short version: Phases 1 through 5 are complete. The repository has a buildable .NET 10 microservice foundation, durable Watchlist management through Telegram, and a confirmed-block ERC-20 indexer with durable checkpoints and transactional outbox recording. Broker integration and the Notification service's local Watchlist projection begin in Phase 6.

**Thesis topic:** Models for organizing monitoring of distributed ledgers (EVM) based on microservice architecture.

**Status:** Phase 5 completed and verified; Phase 6 is the next implementation step.

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
- Nethereum-backed `eth_blockNumber`, `eth_getLogs`, and ERC-20 metadata calls.
- ERC-20 `Transfer` decoding from indexed `from`/`to` topics with normalized transfer identity.
- Durable per-network checkpoints, processed-transfer deduplication, and transactional transfer outbox recording.
- Adaptive block-range reduction for provider timeouts and range-limit responses.
- Deterministic indexer unit and SQLite persistence integration tests.

### Not Yet Implemented

The project still needs:

- Address-to-transfer matching.
- Reliable notification delivery.
- Message broker integration.
- Idempotency and chain-reorganization handling.
- Full telemetry, operational dashboards, and alerts beyond the baseline health endpoints.
- Docker and deployment configuration.
- Business unit, integration, contract, resilience, and performance tests.
- Reproducible experiments and thesis results.

## Target MVP Behavior

The MVP should support this flow:

1. A Telegram user starts the bot.
2. The user sends `/add <network> <0x-address> [label]`.
3. The application validates and normalizes the EVM address.
4. The watchlist service saves the monitored address in its database.
5. A watchlist event updates the notification service's local address projection.
6. The EVM indexer retrieves confirmed blocks and ERC-20 `Transfer` logs through JSON-RPC.
7. The indexer decodes the indexed `from` and `to` topics and publishes a normalized transfer event.
8. The notification service checks both transfer participants against active watchlists.
9. A matching transfer produces a Telegram notification request.
10. The Telegram gateway sends the alert while respecting Telegram rate limits and retry rules.

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
- Decoding and normalizing transfer logs.
- Maintaining a durable checkpoint for every network.
- Detecting chain reorganizations and rewinding safely.
- Publishing normalized ERC-20 transfer events.

Owns indexer checkpoints, processed log identities, recent block hashes, and token metadata cache data.

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

Goal: connect independently owned services without sharing databases.

Tasks:

- Add versioned integration-event envelopes with:
  - `EventId`
  - `EventType`
  - `EventVersion`
  - `OccurredAt`
  - `CorrelationId`
  - `CausationId`
- Define contracts for watchlist changes, observed transfers, orphaned transfers, and Telegram delivery requests.
- Configure RabbitMQ exchanges, queues, retry queues, and dead-letter queues.
- Build the notification service's local watchlist projection from watchlist events.
- Make projection consumers idempotent.
- Add replay support so a projection can be rebuilt without reading the Watchlist database.

Recommended tests:

- Contract compatibility tests reject breaking changes.
- Duplicate events do not duplicate projected addresses.
- Replaying events produces the same final projection.
- A poisoned event moves to the dead-letter queue without blocking later messages.

## Phase 7: Match ERC-20 Transfers

Goal: create one logical notification for every relevant transfer and subscription.

Tasks:

- Compare both normalized `from` and `to` addresses with the local watchlist projection.
- Classify matches as incoming, outgoing, or self-transfer.
- Support multiple users monitoring the same address.
- Resolve the matching label and network display information.
- Convert the raw token value using token decimals.
- Create a stable notification identity from subscription ID, chain ID, transaction hash, and log index.
- Persist the notification before publishing a Telegram delivery request.
- Do not require a USD price feed for the initial ERC-20 monitoring MVP.

Recommended tests:

- A match occurs when the monitored address appears in `topic1`.
- A match occurs when the monitored address appears in `topic2`.
- An unrelated Transfer log is ignored.
- Multiple subscriptions produce separate user notifications.
- Duplicate broker delivery does not create duplicate logical notifications.

## Phase 8: Render and Dispatch Telegram Notifications

Goal: send compact, verifiable alerts without overwhelming Telegram.

Tasks:

- Render wallet label, direction, network, token amount, token contract, and confirmation state.
- Add transaction and address explorer links from network configuration.
- Escape Telegram formatting correctly.
- Add configurable global and per-chat rate limiters.
- Respect Telegram `RetryAfter` responses.
- Retry transient errors with bounded exponential backoff and jitter.
- Move permanently failed delivery requests to a dead-letter queue.
- Persist delivery attempts and final status.

Recommended tests:

- Incoming and outgoing notifications render correctly.
- Unknown token metadata still produces a useful message.
- Markdown or HTML special characters are escaped.
- Rate limits do not exceed configured throughput.
- Transient Telegram failures do not stop the consumer.

## Phase 9: Add Idempotency and Reorganization Handling

Goal: remain correct under at-least-once delivery, service restarts, and EVM chain reorganizations.

Tasks:

- Identify a transfer by `chainId + transactionHash + logIndex`.
- Identify a notification by `subscriptionId + chainId + transactionHash + logIndex`.
- Add inbox tables or equivalent consumer deduplication where needed.
- Store recent canonical block hashes inside a configurable reorganization window.
- Compare stored block hashes before advancing the checkpoint.
- On mismatch, find the common ancestor and rewind the indexer.
- Mark orphaned transfers and publish `TransferOrphaned` events.
- Suppress unconfirmed notifications according to confirmation depth.
- Send a correction only if a previously notified transfer becomes orphaned.

Recommended tests:

- Broker redelivery does not duplicate domain effects.
- Indexer restart does not miss confirmed transfers.
- A simulated short reorganization rewinds to the correct ancestor.
- Orphaned transfers are removed from active notification state.

## Phase 10: Configuration, Security, and Observability

Goal: make live behavior diagnosable and configuration safe.

Tasks:

- Add strongly typed options with startup validation.
- Configure secrets through user secrets locally and environment variables in containers.
- Never commit Telegram tokens, RPC credentials, or database passwords.
- Add structured logging with trace and correlation IDs.
- Redact Telegram user data and provider credentials from logs.
- Add metrics for:
  - current chain head;
  - last processed block;
  - chain lag;
  - RPC request duration and failures;
  - logs decoded;
  - watchlist matches;
  - broker queue depth;
  - notifications sent and failed.
- Add distributed tracing across HTTP requests and broker messages.
- Create Grafana dashboards and basic alerts.

Recommended tests:

- Missing required settings fail fast with a useful error.
- Sensitive values do not appear in logs.
- A transfer can be traced from EVM log identity to Telegram delivery status.

## Phase 11: Dockerize and Deploy the MVP

Goal: run the complete system predictably from a clean checkout.

Tasks:

- Add one Dockerfile per deployable service.
- Add `.dockerignore`.
- Add `compose.yaml` for:
  - Watchlist API;
  - EVM Indexer Worker;
  - Notification Worker;
  - Telegram Gateway Worker;
  - PostgreSQL databases;
  - RabbitMQ;
  - Prometheus and Grafana.
- Add health checks and startup dependency handling.
- Document database migration commands.
- Document local, testnet, and demonstration configuration.
- Add optional Kubernetes manifests only after Docker Compose is stable.

Recommended tests:

- A clean environment starts with one documented command.
- Services recover after RabbitMQ and PostgreSQL restarts.
- Migrations can be applied without rebuilding application images.

## Phase 12: Integration, Resilience, and Performance Testing

Goal: validate failures and service interactions that unit tests cannot cover.

Tasks:

- Use Testcontainers for PostgreSQL and RabbitMQ integration tests.
- Use Anvil and a test ERC-20 contract for deterministic chain scenarios.
- Add an end-to-end test from token transfer to a fake Telegram adapter.
- Add failure drills for:
  - RPC outage;
  - broker restart;
  - database restart;
  - service crash;
  - duplicate event;
  - poisoned event;
  - Telegram throttling;
  - chain reorganization.
- Add performance tests for increasing event rates and watchlist sizes.
- Record recovery time, duplicate rate, missed-event rate, and resource use.

Recommended tests:

- No confirmed transfers are missed during controlled restart tests.
- No duplicate logical notifications appear during redelivery tests.
- Backlogged events are processed after dependency recovery.
- The system reaches the documented reference throughput.

## Phase 13: Thesis Experiments and Documentation

Goal: evaluate architectural models rather than only demonstrate a working bot.

Experiments:

1. Compare broad ERC-20 Transfer-log scanning with dynamically filtered `from` and `to` topic queries as watchlist size grows.
2. Compare block-range sizes and indexer concurrency by RPC volume, throughput, latency, and provider failures.
3. Measure notification latency at different confirmation depths.
4. Measure backlog recovery with different consumer counts.
5. Inject duplicate events, dependency outages, restarts, and reorganizations and measure correctness and recovery time.
6. Compare the microservice solution with a modular-monolith baseline or analytical model by coupling, deployability, fault isolation, resource use, and operational complexity.

Tasks:

- Define hypotheses before running experiments.
- Store benchmark scripts with the repository.
- Record hardware, software, network, and provider conditions.
- Save raw results under `docs/thesis/results/`.
- Separate measured conclusions from architectural reasoning.
- Document threats to validity and experiment limitations.
- Complete C4 diagrams, ADRs, OpenAPI, AsyncAPI, runbooks, and thesis chapters.

## Suggested Next Concrete Step

The best next implementation step is Phase 6, event transport and Watchlist projection:

1. Add a common versioned event envelope with correlation and causation metadata.
2. Configure RabbitMQ exchanges, queues, retries, and dead-letter queues.
3. Dispatch the Watchlist and transfer outboxes with publisher confirmation.
4. Build the Notification service's idempotent local Watchlist projection.
5. Add contract compatibility, duplicate-consumption, replay, and poison-message tests.

This will connect the durable Watchlist and transfer outboxes to independently deployable consumers without cross-service database reads.

## MVP Completion Checklist

### Planning and Architecture

- [x] Define project purpose and MVP scope.
- [x] Define target microservice boundaries.
- [x] Describe the proposed repository structure.
- [x] Define implementation phases and thesis experiments.
- [ ] Create C4 context, container, and component diagrams.
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
- [ ] Reorganization detection and rewind.
- [ ] Idempotent transfer publication.

### Notifications

- [ ] Watchlist projection in the Notification service.
- [ ] Incoming and outgoing address matching.
- [ ] Notification persistence and deduplication.
- [ ] Explorer-linked Telegram rendering.
- [ ] Global and per-chat rate limiting.
- [ ] Retry and dead-letter handling.
- [ ] Orphaned-transfer correction behavior.

### Platform and Quality

- [ ] RabbitMQ integration.
- [ ] PostgreSQL database ownership per service.
- [ ] OpenTelemetry logs, metrics, and traces.
- [x] Health and readiness checks.
- [ ] Dockerfiles and Docker Compose.
- [x] CI build and test workflow.
- [x] Architecture boundary tests.
- [x] Unit-test project scaffolds.
- [x] Watchlist domain unit tests.
- [x] Watchlist application handler tests.
- [x] Watchlist persistence integration tests.
- [x] Telegram command and Watchlist HTTP adapter tests.
- [x] EVM indexer domain and pipeline tests.
- [x] EVM indexer persistence integration tests.
- [ ] Unit tests for remaining MVP business behavior.
- [ ] Integration and contract tests for the remaining services.
- [ ] End-to-end test with Anvil.
- [ ] Resilience and performance tests.
- [ ] Deployment and operations documentation.

### Thesis Completion

- [ ] Research hypotheses finalized.
- [ ] Monitoring-model comparison completed.
- [ ] Microservice and modular-monolith comparison completed.
- [ ] Raw benchmark results committed.
- [ ] Results analyzed with threats to validity.
- [ ] Thesis chapters and presentation completed.
- [ ] End-to-end MVP validated on a live EVM network.

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
