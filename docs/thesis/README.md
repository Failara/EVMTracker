# Thesis Research Package

This directory turns the working EVMTracker system into a reproducible research artifact for the thesis topic **“Models for organizing monitoring of distributed ledgers (EVM) based on microservice architecture.”**

Contents:

- `research-plan.md` — research questions, hypotheses, variables, and acceptance rules.
- `methodology.md` — experiment protocol, statistics, reproducibility requirements, and threats to validity.
- `architecture-comparison.md` — microservice versus modular-monolith analytical comparison.
- `thesis-draft.md` — chapter-ready narrative and evidence map.
- `experiments/` — machine-readable experiment definitions and schemas.
- `results/` — immutable raw runs and interpretation guidance.
- `../architecture/c4-model.md` — system context, container, and Indexer component views.
- `../contracts/` — implemented Watchlist OpenAPI and integration-event AsyncAPI specifications.

Run the local experiment suite with:

```powershell
./scripts/run-thesis-experiments.ps1
```

Measurements must be interpreted using the protocol in `methodology.md`. A single local run is a smoke baseline, not evidence of general performance.

The software phases provide the research harness and a documented baseline. Final academic submission still requires repeated measurements on a frozen revision, statistical summaries, scholarly citations, university-specific formatting, and defense slides.
