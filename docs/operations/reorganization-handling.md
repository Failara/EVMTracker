# EVM Reorganization Handling

Phase 9 adds bounded canonical-chain verification to the confirmed ERC-20 indexing pipeline. The indexer still waits for each network's configured confirmation depth, then retains a second bounded safety window of canonical block hashes for detecting deeper reorganizations.

## Configuration

Each `EvmIndexer:Networks` entry has a `ReorganizationWindow`:

```json
{
  "ConfirmationDepth": 12,
  "ReorganizationWindow": 64
}
```

`ConfirmationDepth` suppresses unconfirmed alerts by keeping blocks above the safe head out of the pipeline. `ReorganizationWindow` controls how many recently processed safe blocks are retained for common-ancestor discovery. It must be at least two.

## Detection Algorithm

Before advancing a stored checkpoint, the processor:

1. Loads retained canonical hashes newest-first.
2. Reads the current hash for the newest retained block.
3. Continues immediately when that hash still matches; a matching descendant commits the ancestry below it.
4. On mismatch, walks retained hashes backwards until it finds the common ancestor.
5. Rewinds to the block after that ancestor.

If all retained hashes differ and the window does not reach the configured `StartBlock`, the processor throws `ReorganizationWindowExceededException` and leaves the checkpoint unchanged. This fails safe rather than silently accepting a reorganization deeper than the configured evidence window.

The processor also reads current hashes for the retained tail and every block containing a returned transfer. If a log's block hash differs from the canonical hash after `eth_getLogs` completes, the iteration returns `ChainChangedDuringRead` without committing transfers or advancing the checkpoint.

## Atomic Rewind

Indexer rewind uses one database transaction to:

- reset the network checkpoint;
- delete invalid canonical hash records;
- mark affected processed transfers as orphaned;
- cancel observed-transfer outbox rows that have not been published;
- write deterministic `transfers.erc20-orphaned.v1` outbox events.

Orphan events are emitted even when an observation is cancelled. This closes the unavoidable race where an outbox publisher may already have transmitted an observation immediately before the rewind transaction marks it cancelled.

Transfer identity remains `chainId + transactionHash + logIndex`. Observation event identity additionally includes the block hash, allowing the same logical transfer to become active again when it reappears in a replacement canonical block.

## Notification State

Notification consumes observed and orphaned events from the same durable transfer queue. Both paths use the existing inbox for broker-event deduplication.

On an orphan event, Notification:

- stores a logical orphan tombstone keyed by transfer identity and former block hash;
- marks matching active notification records orphaned;
- cancels an original alert whose delivery outbox has not been published;
- creates one Telegram correction when the original delivery request was already published.

The tombstone suppresses a stale observation that arrives after its orphan event. An observation with the same transfer identity and a different block hash reactivates the logical notification and creates a new block-specific delivery event.

“Previously notified” is defined at the Notification service boundary as an original Telegram delivery request already published to RabbitMQ. Telegram's final external-delivery result remains owned by Telegram Gateway.

## Correction Message

A correction identifies the network, wallet, transaction, former block number, and former block hash. It uses the same escaped Telegram HTML and configured explorer links as ordinary transfer alerts.

Correction IDs are deterministic over the logical notification and orphan event. Broker redelivery or an equivalent repeated rewind cannot create a second correction.

## Database Migrations

Apply both service migrations before starting Phase 9 code:

```powershell
dotnet tool run dotnet-ef database update `
  --project src/Services/EvmIndexer/EVMTracker.EvmIndexer.Infrastructure `
  --startup-project src/Services/EvmIndexer/EVMTracker.EvmIndexer.Infrastructure `
  --context IndexerDbContext

dotnet tool run dotnet-ef database update `
  --project src/Services/Notification/EVMTracker.Notification.Infrastructure `
  --startup-project src/Services/Notification/EVMTracker.Notification.Infrastructure `
  --context NotificationDbContext
```

Existing installations cannot reconstruct historical block hashes during migration. The first successful post-upgrade indexing iteration establishes the retained canonical baseline; hash comparison protects subsequent checkpoint advances.

## Operational Response

When `ReorganizationWindowExceededException` appears:

1. Pause the affected network worker.
2. Verify RPC provider consistency and the network's canonical chain.
3. Choose a trusted rewind block at or below the common ancestor.
4. Increase `ReorganizationWindow` if the network's finality characteristics require it.
5. Resume and verify checkpoint, orphan-event, correction, and chain-lag telemetry.

Do not manually advance the checkpoint past an unresolved mismatch.
