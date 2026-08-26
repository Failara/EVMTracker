using EVMTracker.TelegramGateway.Application.Delivery;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace EVMTracker.TelegramGateway.Infrastructure.Telegram;

internal sealed class TelegramMessageSender(ITelegramBotClient botClient)
    : ITelegramMessageSender
{
    public async Task<TelegramSendResult> SendAsync(
        long chatId,
        string message,
        string parseMode,
        CancellationToken cancellationToken)
    {
        if (!TryParseMode(parseMode, out ParseMode telegramParseMode))
        {
            return TelegramSendResult.PermanentFailure(
                $"Telegram parse mode '{parseMode}' is unsupported.");
        }

        try
        {
            Message sent = await botClient.SendMessage(
                chatId,
                message,
                parseMode: telegramParseMode,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return TelegramSendResult.Success(sent.Id);
        }
        catch (ApiRequestException exception) when (
            exception.Parameters?.RetryAfter is int retryAfter)
        {
            return TelegramSendResult.TransientFailure(
                exception.Message,
                TimeSpan.FromSeconds(retryAfter));
        }
        catch (ApiRequestException exception) when (
            exception.ErrorCode == 429 || exception.ErrorCode >= 500)
        {
            return TelegramSendResult.TransientFailure(exception.Message);
        }
        catch (ApiRequestException exception)
        {
            return TelegramSendResult.PermanentFailure(exception.Message);
        }
        catch (RequestException exception)
        {
            return TelegramSendResult.TransientFailure(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return TelegramSendResult.TransientFailure(exception.Message);
        }
    }

    private static bool TryParseMode(string value, out ParseMode parseMode)
    {
        if (string.Equals(value, "HTML", StringComparison.OrdinalIgnoreCase))
        {
            parseMode = ParseMode.Html;
            return true;
        }

        if (string.Equals(value, "PlainText", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
        {
            parseMode = ParseMode.None;
            return true;
        }

        parseMode = ParseMode.None;
        return false;
    }
}
