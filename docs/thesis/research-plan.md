# Research Questions and Hypotheses

## Objective

Evaluate how an event-driven microservice architecture organizes EVM address monitoring and what operational tradeoffs arise from acquisition strategy, confirmation policy, batching, and asynchronous delivery.

## Research questions

### RQ1 — Log acquisition model

How does broad ERC-20 `Transfer` scanning compare with tracked-address topic filtering as irrelevant network traffic and watchlist size increase?

- **H1a:** Topic filtering reduces returned irrelevant logs and downstream writes by at least one order of magnitude when relevant transfers are sparse.
- **H1b:** Filter construction and RPC processing cost increase with watchlist size, eventually requiring address partitioning or multiple queries.
- **Null:** Filtering does not materially reduce end-to-end resource use or produces equivalent provider load.

### RQ2 — Block-range strategy

How does requested block-range size affect catch-up throughput, latency, and provider failure rate?

- **H2:** Larger ranges improve throughput until RPC response size or provider limits dominate; adaptive range reduction gives better recovery than one fixed range.
- **Null:** Range size has no repeatable effect on throughput or failure rate.

### RQ3 — Confirmation policy

What latency/finality tradeoff results from different confirmation depths?

- **H3:** Minimum detection latency grows approximately linearly with confirmation depth and average block time, while reorganization exposure decreases.
- **Null:** Confirmation depth does not materially affect latency or correction frequency.

### RQ4 — Failure recovery

Can durable queues, checkpoints, inboxes, and outboxes recover from downtime without missed or duplicate logical notifications?

- **H4:** After dependency recovery, every accepted event reaches one terminal logical outcome, with zero misses and zero duplicate domain effects.
- **Null:** Restarts cause loss, unbounded duplicate effects, or unrecoverable backlog.

### RQ5 — Architectural organization

When is the microservice model preferable to a modular monolith for this workload?

- **H5:** Microservices improve fault isolation and independent scaling of indexing and delivery at the cost of higher deployment, consistency, storage, and observability complexity.
- **Null:** The additional distribution produces no meaningful operational benefit for the evaluated scale.

## Variables and metrics

| Dimension | Independent variables | Dependent metrics | Controls |
|---|---|---|---|
| Acquisition | broad/filtered model, watchlist size, irrelevant-log ratio | returned logs, persisted rows, RPC latency, request failures | chain state, transfer count, hardware |
| Indexing | block range, confirmation depth | blocks/s, transfer latency, RPC failures, rewinds | RPC provider, start/end block |
| Delivery | backlog size, consumer count, failure injection | drain time, messages/s, misses, duplicates | broker version, message shape |
| Reliability | outage type and duration | recovery time, terminal outcomes, duplicate effects | seeded event IDs and checkpoints |
| Architecture | deployment model | services, data stores, failure domains, coupling, operational steps | same domain behavior and workload |

## Decision rules

- Correctness experiments fail on any missed logical event or duplicate domain effect.
- Performance conclusions require at least one warm-up and five measured repetitions per case.
- Report median, minimum, maximum, and p95; do not select only the fastest run.
- Compare models under the same hardware, Docker allocation, software versions, and deterministic workload.
- Treat provider throttling and public-network activity as environmental variables, not implementation constants.
- Reject or qualify a hypothesis when confidence intervals overlap materially or the observed effect is not operationally meaningful.
