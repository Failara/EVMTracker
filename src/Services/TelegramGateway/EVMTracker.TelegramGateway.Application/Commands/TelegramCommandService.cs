using System.Text;
using EVMTracker.TelegramGateway.Application.Abstractions;
using EVMTracker.TelegramGateway.Application.Watchlists;

namespace EVMTracker.TelegramGateway.Application.Commands;

public sealed class TelegramCommandService(
    IChainCatalog chainCatalog,
    IWatchlistClient watchlistClient)
{
    private const int MaximumListedAddresses = 20;

    public async Task<string> HandleAsync(
        TelegramCommandRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TelegramUserId <= 0 || request.TelegramChatId == 0)
        {
            return "This command must come from a valid Telegram user and chat.";
        }

        BotCommandParseResult parseResult = BotCommandParser.Parse(request.Text);
        if (!parseResult.IsSuccess)
        {
            return parseResult.Error!;
        }

        return parseResult.Command switch
        {
            BotCommand.Start => CreateWelcomeText(),
            BotCommand.Help => CreateHelpText(),
            BotCommand.Add add => await AddAsync(
                request,
                add,
                cancellationToken).ConfigureAwait(false),
            BotCommand.List => await ListAsync(
                request,
                cancellationToken).ConfigureAwait(false),
            BotCommand.Remove remove => await RemoveAsync(
                request,
                remove,
                cancellationToken).ConfigureAwait(false),
            BotCommand.Status => await StatusAsync(
                request.CorrelationId,
                cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unsupported command type."),
        };
    }

    private async Task<string> AddAsync(
        TelegramCommandRequest request,
        BotCommand.Add command,
        CancellationToken cancellationToken)
    {
        ChainNetwork? network = chainCatalog.Find(command.Network);
        if (network is null)
        {
            return CreateUnsupportedNetworkText(command.Network);
        }

        WatchlistClientResult<TrackedAddressView> result = await watchlistClient.AddAsync(
            new AddTrackedAddressRequest(
                command.Address,
                request.TelegramUserId,
                request.TelegramChatId,
                network.ChainId,
                command.Label),
            request.CorrelationId,
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? $"Now tracking {FormatAddressName(result.Value)} on {network.DisplayName}."
            : MapFailure(result.Failure!, $"Usage: /add {network.Name} <0x-address> [label]");
    }

    private async Task<string> ListAsync(
        TelegramCommandRequest request,
        CancellationToken cancellationToken)
    {
        WatchlistClientResult<IReadOnlyList<TrackedAddressView>> result =
            await watchlistClient.ListAsync(
                request.TelegramUserId,
                request.CorrelationId,
                cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return MapFailure(result.Failure!, "Send /list to try again.");
        }

        TrackedAddressView[] activeAddresses = result.Value
            .Where(address => address.IsActive)
            .Take(MaximumListedAddresses + 1)
            .ToArray();
        if (activeAddresses.Length == 0)
        {
            return "Your watchlist is empty. Use /add <network> <0x-address> [label].";
        }

        var response = new StringBuilder("Your tracked addresses:");
        foreach (TrackedAddressView address in activeAddresses.Take(MaximumListedAddresses))
        {
            response.AppendLine();
            response.Append("• ").Append(FormatAddressName(address));
            response.AppendLine();
            response.Append("  Networks: ").Append(FormatNetworks(address.ChainIds));
        }

        if (activeAddresses.Length > MaximumListedAddresses)
        {
            response.AppendLine();
            response.Append("Showing the first ").Append(MaximumListedAddresses).Append(" entries.");
        }

        return response.ToString();
    }

    private async Task<string> RemoveAsync(
        TelegramCommandRequest request,
        BotCommand.Remove command,
        CancellationToken cancellationToken)
    {
        ChainNetwork? network = chainCatalog.Find(command.Network);
        if (network is null)
        {
            return CreateUnsupportedNetworkText(command.Network);
        }

        WatchlistClientResult<bool> result = await watchlistClient.RemoveNetworkAsync(
            new RemoveTrackedAddressNetworkRequest(
                command.Address,
                request.TelegramUserId,
                network.ChainId),
            request.CorrelationId,
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? $"Stopped tracking {command.Address} on {network.DisplayName}."
            : MapFailure(result.Failure!, $"Usage: /remove {network.Name} <0x-address>");
    }

    private async Task<string> StatusAsync(
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        WatchlistClientResult<WatchlistServiceStatus> result =
            await watchlistClient.GetStatusAsync(
                correlationId,
                cancellationToken).ConfigureAwait(false);

        return result.IsSuccess && result.Value.IsAvailable
            ? "EVMTracker is running and the Watchlist service is available."
            : "EVMTracker is running, but the Watchlist service is temporarily unavailable.";
    }

    private string CreateWelcomeText() =>
        "Welcome to EVMTracker. I monitor ERC-20 activity for your EVM addresses.\n\n" +
        CreateHelpText();

    private string CreateHelpText() =>
        "Commands:\n" +
        "/add <network> <0x-address> [label]\n" +
        "/list\n" +
        "/remove <network> <0x-address>\n" +
        "/status\n" +
        "/help\n\n" +
        "Networks: " + string.Join(
            ", ",
            chainCatalog.Networks.Select(network => network.Name));

    private string CreateUnsupportedNetworkText(string network) =>
        $"Unsupported network '{network}'. Supported networks: " +
        string.Join(", ", chainCatalog.Networks.Select(item => item.Name)) + ".";

    private string FormatNetworks(IEnumerable<long> chainIds) => string.Join(
        ", ",
        chainIds.Select(
            chainId => chainCatalog.Networks
                .FirstOrDefault(network => network.ChainId == chainId)?.DisplayName
                ?? $"chain {chainId}"));

    private static string FormatAddressName(TrackedAddressView address) =>
        string.IsNullOrWhiteSpace(address.Label)
            ? address.Address
            : $"{address.Label} ({address.Address})";

    private static string MapFailure(
        WatchlistClientFailure failure,
        string validationGuidance) => failure.Error switch
        {
            WatchlistClientError.Validation =>
                $"The address or command parameters are invalid. {validationGuidance}",
            WatchlistClientError.Duplicate =>
                "That address is already tracked on this network.",
            WatchlistClientError.NotFound =>
                "That address is not tracked on this network.",
            WatchlistClientError.Unauthorized =>
                "You cannot change another user's tracked address.",
            WatchlistClientError.Unavailable =>
                "The Watchlist service is temporarily unavailable. Please try again shortly.",
            _ => "The command could not be completed. Please try again.",
        };
}
