# Thesis Draft and Evidence Map

## 1. Introduction

Distributed ledgers provide transparent event histories but do not directly provide user-specific monitoring, filtering, durable delivery, or notification workflows. This thesis investigates a microservice model for monitoring user-selected EVM addresses through ERC-20 `Transfer` logs and delivering matched activity through Telegram.

The engineering problem combines high-volume external data, unreliable network dependencies, chain reorganizations, at-least-once messaging, user-owned configuration, and third-party delivery limits. The research contribution is a concrete architecture and a reproducible evaluation of acquisition, batching, confirmation, and recovery models.

## 2. Conceptual background

The domain is modeled around tracked EVM addresses, confirmed log observations, notification matches, and delivery attempts. Domain-driven design isolates invariants inside service boundaries. Event-driven integration reduces temporal coupling but introduces eventual consistency and duplicate delivery. Inbox/outbox patterns, idempotent identities, durable checkpoints, and reorganization corrections are used to recover correctness.

## 3. Requirements

The MVP must let a Telegram user add, list, and remove EVM addresses; monitor configured networks; detect confirmed ERC-20 transfers in either direction; and deliver readable Telegram alerts. Quality requirements include restart recovery, secret protection, bounded storage, deterministic deduplication, chain-reorganization handling, observable failure states, and reproducible deployment.

## 4. Architecture

The solution contains Watchlist, EVM Indexer, Notification, and Telegram Gateway services. Each service owns PostgreSQL state. RabbitMQ carries versioned integration events. The Indexer obtains a local watchlist projection before advancing checkpoints and issues topic-filtered `eth_getLogs` requests. The Notification service matches transfers against its own projection and creates delivery requests. Telegram Gateway applies rate limits and persists every terminal outcome.

The context, container, and component views are documented in `../architecture/c4-model.md`; decisions are recorded in `../adr/`.

## 5. Implementation

The system targets .NET 10 and uses EF Core, Nethereum, MassTransit, PostgreSQL, RabbitMQ, Telegram.Bot, OpenTelemetry, Prometheus, Grafana, Tempo, Docker Compose, xUnit, and Testcontainers. Clean Architecture projects separate domain, application, infrastructure, and host responsibilities. Service-owned migrations, health checks, structured redacted logs, metrics, traces, retention workers, and bounded container logs support operation.

An initial BSC capacity run showed that broad chain-wide `Transfer` ingestion accumulated hundreds of thousands of irrelevant rows and could not catch the chain head. Replacing it with tracked-address topic filters reduced the durable state to relevant observations and enabled fast checkpoint recovery.

## 6. Experimental method

The research questions and hypotheses are defined in `research-plan.md`. The deterministic local protocol uses pinned Testcontainers and Anvil; live-chain runs provide ecological validation. Correctness is evaluated through misses, duplicate logical effects, terminal delivery status, rewind behavior, and durable recovery. Performance is evaluated through returned-log volume, query latency, backlog drain rate, and storage/resource observations.

## 7. Baseline results

The first local smoke baseline is stored under `results/`. With 50 irrelevant logs and one relevant transfer, broad acquisition returned 51 logs while address filtering returned one. Backlogs of 100 and 1,000 messages drained with zero misses and zero duplicates in the recorded run. Address-count and block-range probes returned the same single relevant log across their tested bounds. Confirmation-depth modeling produced minimum added latency of 0, 9, and 36 seconds at an assumed three-second block time for depths 0, 3, and 12.

These values validate the harness and expected directions; they are not publication-grade statistical results because the baseline contains one measured repetition per case and uses an in-memory local chain.

## 8. Discussion

The strongest observed architectural effect is reduction of irrelevant downstream work. Topic filtering can take longer than a broad local query in a tiny Anvil dataset because it performs separate incoming and outgoing requests; nevertheless, it prevents irrelevant logs from entering decoding, metadata resolution, persistence, messaging, and notification services. This distinction illustrates why end-to-end resource cost matters more than a single RPC timing.

Durable broker and database restart drills support the correctness hypothesis under controlled failures. The microservice architecture achieves fault isolation and independent recovery, but requires substantially more explicit infrastructure and testing than the analytical modular-monolith baseline.

## 9. Conclusion

EVMTracker demonstrates a viable microservice model for EVM address monitoring. Its key mechanisms are service-owned state, filtered confirmed-log acquisition, durable checkpoints, versioned events, inbox/outbox delivery, idempotent logical identities, bounded reorganization recovery, and rate-limited Telegram delivery. The architecture is appropriate when independent scaling and fault isolation justify its operational cost; a modular monolith remains a credible lower-complexity choice for smaller deployments.

Before submission, replace the smoke baseline with repeated measurements following `methodology.md`, add statistical summaries and figures, and align terminology and citation style with university requirements.
