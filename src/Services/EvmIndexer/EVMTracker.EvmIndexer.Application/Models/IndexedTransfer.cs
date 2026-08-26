using EVMTracker.Contracts.Transfers;
using EVMTracker.EvmIndexer.Domain.Transfers;

namespace EVMTracker.EvmIndexer.Application.Models;

public sealed record IndexedTransfer(
    Erc20Transfer Transfer,
    TokenMetadata TokenMetadata,
    Erc20TransferObservedV1 IntegrationEvent);
