using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Application.Contracts;

namespace VaultHistory.Notification.Application.Abstractions;

public interface ITemplateRenderer
{
    Task<Result<string>> RenderAsync(EmailTemplate template, EmailTemplateModel model, CancellationToken cancellationToken);
}
