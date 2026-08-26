# ADR 0004: Confirmed block polling with eth_getLogs

- Status: Accepted
- Date: 2026-08-26

## Context

WebSocket subscriptions provide low latency but do not by themselves provide durable replay or recovery after disconnects. The system must resume deterministically and account for EVM chain reorganizations.

## Decision

Use block polling as the authoritative acquisition model. For each network, read the head, subtract a configurable confirmation depth, and retrieve bounded ranges with `eth_getLogs` filtered by the ERC-20 `Transfer` signature.

Persist per-network checkpoints and recent block hashes. Decode wallet participants from indexed topics, because the log address is the token contract. Adapt range size and retry transient provider failures. A later thesis experiment compares broad Transfer-log scanning with dynamically filtered participant-topic queries.

## Consequences

- Restart and gap recovery are deterministic.
- Notification latency includes polling and confirmation delays.
- Provider limits require adaptive ranges, throttling, and metrics.
- Reorganization handling is a first-class indexer responsibility.
