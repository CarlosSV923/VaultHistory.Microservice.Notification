using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.Gmail;

public sealed class GmailEmailSender(
    IGmailMessageClient gmailClient,
    IOptions<GmailOptions> options,
    ILogger<GmailEmailSender> logger) : IEmailSender
{
    public async Task<Result> SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var raw = GmailMimeMessageBuilder.CreateRaw(settings.SenderAddress, settings.SenderName, recipient, subject, htmlBody);
            var messageId = await gmailClient.SendRawAsync(raw, cancellationToken);
            logger.LogInformation("Gmail accepted notification email with technical message ID {MessageId}.", messageId);
            return Result.Success();
        }
        catch (FormatException exception)
        {
            return Result.Failure(new Error("gmail.invalid_recipient", exception.Message));
        }
        catch (GmailDeliveryException exception)
        {
            logger.LogWarning(exception, "Gmail delivery failed with code {Code}.", exception.Code);
            return Result.Failure(new Error(exception.Code, exception.Message));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(new Error("gmail.timeout", "Gmail delivery timed out."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Gmail delivery failed unexpectedly.");
            return Result.Failure(new Error("gmail.send_failed", "Gmail delivery failed."));
        }
    }
}
