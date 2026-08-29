# Experimental Methodology

## Environments

Use two complementary environments:

1. **Deterministic local environment:** pinned PostgreSQL, RabbitMQ, and Foundry Anvil Testcontainers. This environment is used for correctness, repeatability, controlled outages, and relative performance comparisons.
2. **External EVM environment:** a documented BSC or EVM-compatible RPC endpoint. This environment is used only for ecological validation because network load, provider policies, and chain traffic are not controlled.

Never include Telegram tokens, RPC credentials, database passwords, chat identifiers, or full provider URLs in committed results.

## Protocol

For each measured case:

1. Record the Git revision and whether the working tree contains changes.
2. Record CPU, logical processors, RAM, OS, .NET SDK/runtime, Docker version, Docker memory allocation, and container image tags.
3. Start from disposable test dependencies or a documented database/checkpoint snapshot.
4. Run one unrecorded warm-up.
5. Run at least five measured repetitions in randomized case order where practical.
6. Save raw per-run output before calculating summaries.
7. Report failures and outliers; never silently discard them.
8. Verify zero misses and duplicate logical effects before interpreting throughput.

## Derived statistics

For latency and throughput report:

- sample count;
- median;
- arithmetic mean;
- minimum and maximum;
- p95 using the nearest-rank method;
- standard deviation;
- correctness counters for misses, duplicate deliveries, and terminal failures.

The local performance probes intentionally have no fixed throughput threshold because CI hosts and student hardware vary. Assertions protect correctness, while raw values support within-machine comparisons.

## Experiment mapping

| Experiment | Automated artifact | Primary outputs |
|---|---|---|
| Broad versus filtered logs | `FilteredModelExcludesIrrelevantLogsComparedWithBroadScan` | returned logs and query time |
| Watchlist scaling | `FilteredLogQueryScalesAcrossTrackedAddressCounts` | address count and query time |
| Block-range scaling | `FilteredQueryRecordsBlockRangeScaling` | range and query time |
| Confirmation depth | `ConfirmationDepthModelTests` | minimum modeled latency |
| Backlog recovery | `RabbitMqBacklogPerformanceTests` | drain time, throughput, misses, duplicates, memory delta |
| Dependency recovery | container integration category | pass/fail, recovery and durable state |
| Reorganization | Indexer and Notification integration tests | rewinds, tombstones, corrections, idempotency |

## Threats to validity

### Internal validity

- JIT compilation, image cold starts, antivirus activity, and concurrent desktop workloads can distort short measurements.
- Test-process working set is not total platform memory; container metrics should be added for publication-grade resource analysis.
- Anvil mines instantly and does not reproduce public-provider throttling or BSC execution load.

Mitigation: warm-ups, repeated randomized trials, pinned images, clean disposable state, and separate correctness from performance assertions.

### External validity

- One BSC provider does not represent all EVM networks or commercial RPC policies.
- A small watchlist does not establish behavior at institutional scale.
- Telegram delivery behavior varies with regional networking and API limits.

Mitigation: publish exact workload bounds, repeat with at least two provider classes when possible, and avoid universal claims.

### Construct validity

- Returned log count is a proxy for downstream work, not a complete measurement of provider CPU or network bytes.
- Service count and deployment steps are proxies for operational complexity.

Mitigation: triangulate logs, database growth, queue metrics, traces, and operator-step counts.

### Conclusion validity

- A single baseline cannot support statistical inference.
- Very short timings have high relative noise.

Mitigation: use five or more repetitions, report distributions, and distinguish observed facts from architectural inference.
