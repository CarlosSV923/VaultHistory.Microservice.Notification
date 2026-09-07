namespace VaultHistory.Notification.Infrastructure.Gmail;

public interface IGmailMessageClient
{
    Task<string> SendRawAsync(string rawMessage, CancellationToken cancellationToken);
}
