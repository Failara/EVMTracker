# ERC-20 transfer matching

The Notification service consumes `TransferIntegrationEventEnvelope` messages from the durable `notification-transfer-matcher` queue. It never queries the EVM Indexer or Watchlist databases.

## Matching model

For a confirmed ERC-20 transfer, the application queries the local projection using `(chainId, fromAddress, toAddress)`. Every matching projected subscription is handled independently:

- address equals only `toAddress`: `Incoming`;
- address equals only `fromAddress`: `Outgoing`;
- address equals both: `Self`.

An unrelated transfer writes only its inbox marker. This prevents repeated broker deliveries from repeating the same lookup while avoiding unnecessary notification rows.

## Identity and transaction boundary

The logical notification ID is the SHA-256-derived UUID of:

```text
subscriptionId : chainId : normalizedTransactionHash : logIndex
```

The database also enforces a unique constraint over those four source fields. In one transaction, Notification writes:

1. the transfer notification;
2. a versioned Telegram delivery-request outbox row;
3. the source transfer's inbox marker.

The inbox handles ordinary broker redelivery. The stable identity and unique index also reject an equivalent transfer that arrives with a different event ID.

The Notification outbox dispatcher is active because Telegram Gateway now declares the durable `telegram-delivery` queue. Pending delivery requests publish through the transactional outbox and are tracked in the gateway's delivery ledger.

## Token amounts

Token amounts use `BigInteger` and decimal-string manipulation. Floating-point values are never used. If decimals are unavailable, the raw unsigned integer is retained for display. USD valuation is intentionally outside the initial MVP.

## Eventual consistency

The transfer matcher reads the current local Watchlist projection. Watchlist and transfer events are separate ordered streams, so an event arriving during projection lag is an eventual-consistency edge case to measure during the resilience phases. The service records enough stable identity data to replay a transfer safely without duplicating a logical notification.
