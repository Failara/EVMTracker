namespace EVMTracker.TelegramGateway.Application.Abstractions;

public sealed record ChainNetwork(string Name, string DisplayName, long ChainId);
