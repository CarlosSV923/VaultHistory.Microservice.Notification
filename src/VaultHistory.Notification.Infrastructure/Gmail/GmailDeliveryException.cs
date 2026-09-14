namespace VaultHistory.Notification.Infrastructure.Gmail;

public sealed class GmailDeliveryException(string code, string description, Exception? innerException = null) : Exception(description, innerException)
{
    public string Code { get; } = code;
}
