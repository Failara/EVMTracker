# Microservice and Modular-Monolith Comparison

The implemented system is compared with an analytical modular-monolith baseline containing the same Watchlist, Indexer, Notification, and Telegram modules in one deployable process and one operational database server. The baseline preserves module boundaries but replaces RabbitMQ integration events with in-process dispatch.

| Criterion | Implemented microservices | Modular-monolith baseline | Interpretation |
|---|---|---|---|
| Deployment units | Four services plus infrastructure | One application plus database | Monolith is simpler for an MVP |
| Data ownership | Separate schema/database per service | Shared deployment, preferably schemas per module | Microservices enforce ownership more strongly |
| Communication | Versioned asynchronous events and Watchlist HTTP API | In-process calls/events | Monolith avoids serialization and broker failure modes |
| Consistency | Local transactions plus inbox/outbox and eventual consistency | Cross-module local transaction is possible | Microservices require explicit convergence behavior |
| Failure isolation | Indexer, notification, and Telegram delivery can fail independently | Process failure affects all modules | Microservices are stronger under partial failure |
| Scaling | Indexer and delivery consumers scale independently | Entire process scales together | Microservices suit asymmetric chain workloads |
| Recovery | Durable checkpoints, queues, inboxes, and outboxes | Local queues/checkpoints still required for durability | Broker adds capability and operational cost |
| Observability | Distributed traces, correlation IDs, queue metrics | Primarily process-local traces | Microservices need richer telemetry |
| Resource floor | Four runtimes, four pools, broker, multiple databases | One runtime and fewer connections | Monolith is more efficient at small scale |
| Change coupling | Contract-compatible independent releases are possible | Modules release together | Benefit depends on team and release independence |
| Testing | Contract, container, and failure-injection tests | More in-process integration tests | Distributed correctness costs more to verify |
| Thesis value | Exposes distributed-system tradeoffs directly | Provides a lower-complexity comparison model | Both are needed for a balanced conclusion |

## Analytical conclusion

For a single operator and a small watchlist, the modular monolith is likely the economically preferable starting model because it minimizes infrastructure and operational burden. The microservice model becomes defensible when indexing load, delivery reliability, independent scaling, fault isolation, or separate team ownership are first-class requirements. EVMTracker therefore demonstrates not that microservices are universally superior, but how their mechanisms—checkpoints, inbox/outbox, durable queues, service-owned data, and observability—address specific distributed-ledger monitoring risks.

This conclusion is an architectural inference. Phase 13 measurements quantify selected runtime effects, but a full empirical modular-monolith implementation would be required for causal performance comparison.
