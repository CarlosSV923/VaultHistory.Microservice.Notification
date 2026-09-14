using VaultHistory.Notification.Domain.Abstractions;

namespace VaultHistory.Notification.Application.Abstractions;

public interface IEmailSender
{
    Task<Result> SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken);
}
