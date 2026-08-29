# Phase 12 Integration, Resilience, and Performance Testing

Phase 12 uses disposable Docker dependencies to verify behavior that SQLite and unit tests cannot reproduce. The tests use [Testcontainers for .NET](https://dotnet.testcontainers.org/) with pinned PostgreSQL, RabbitMQ, and [Foundry Anvil](https://getfoundry.sh/anvil/overview/) images.

The containers are isolated from `compose.yaml`. They use random host ports, temporary storage, and Testcontainers cleanup labels, so the tests do not read or mutate the live development databases, queues, watchlist, RPC configuration, or Telegram token.

## Prerequisites

- .NET 10 SDK.
- Docker Desktop or another reachable Linux Docker engine.
- Enough Docker storage for the pinned dependency images.

The first run downloads missing images. Later runs reuse the images, while the individual test containers and their volumes are removed automatically.

## Run the suite

Run all Phase 12 tests:

```powershell
./scripts/run-phase12-tests.ps1
```

Run only infrastructure and recovery drills:

```powershell
./scripts/run-phase12-tests.ps1 -Mode Infrastructure
```

Run only repeatable performance probes:

```powershell
./scripts/run-phase12-tests.ps1 -Mode Performance
```

The same tests remain part of the normal solution test command:

```powershell
dotnet test EVMTracker.sln --configuration Release
```

## Automated scenarios

### PostgreSQL

- Applies the real EVM Indexer EF Core migrations to PostgreSQL.
- Commits a transfer, checkpoint, canonical block, and outbox record atomically.
- Replays the same transfer and verifies that no duplicate domain or outbox row appears.
- Stops and restarts PostgreSQL and verifies that the checkpoint and records survive.

### RabbitMQ

- Declares a durable consumer queue, takes the consumer offline, and queues work.
- Restarts RabbitMQ and verifies that the durable backlog is delivered afterward.
- Retries a deliberately poisoned message and verifies that MassTransit moves it to the durable `_error` queue.
- Drains 100-message and 1,000-message backlogs while recording elapsed time, throughput, duplicates, misses, and test-process working-set change.

### Anvil and notification flow

- Starts an isolated Anvil chain with deterministic funded accounts.
- Installs a minimal EVM runtime that emits a standards-compatible ERC-20 `Transfer` log.
- Queries the real node through the production Nethereum log source with tracked-address topic filters.
- Decodes the log, matches the tracked address, renders the alert, and sends it through a fake Telegram adapter.
- Stops Anvil to model an RPC outage, verifies failure, restarts it, and verifies recovery through a fresh worker client.
- Repeats the filtered log query with 1, 100, and 500 tracked addresses and records elapsed time.

### Existing deterministic coverage used by the drills

- Telegram `RetryAfter`, transient failure, retry exhaustion, and permanent-failure behavior.
- Duplicate transfer and notification suppression.
- EVM common-ancestor rewind, orphan notification correction, tombstones, and canonical reappearance.
- Unsupported/poisoned integration-event rejection.

## Interpreting performance output

Performance probes emit one JSON object per case to the test output. These are diagnostic measurements, not machine-independent pass/fail thresholds. Correctness assertions require:

- zero missed messages;
- zero duplicate deliveries in the recovery probe;
- exactly one matching EVM log regardless of unrelated watchlist size.

Phase 13 benchmark runs should capture the JSON output together with machine, Docker, RPC, and software-version metadata under `docs/thesis/results/` before drawing performance conclusions.
