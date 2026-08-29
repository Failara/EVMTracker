# Experiment Results

This directory stores immutable, non-secret experiment outputs. Each result must reference an experiment manifest, identify the repository revision and dirty-state, record the environment, retain every raw repetition, and distinguish measured facts from interpretation.

`baseline-2026-08-29.json` is a one-run smoke baseline used to validate the Phase 13 harness. It must not be cited as a statistically supported performance conclusion. Before submission, run the manifest protocol with at least one warm-up and five measured repetitions per case, then report median, mean, minimum, maximum, p95, standard deviation, misses, and duplicate logical effects.

Do not commit Telegram tokens, RPC credentials or full credential-bearing URLs, database passwords, RabbitMQ credentials, Telegram user/chat identifiers, or monitored private addresses.
