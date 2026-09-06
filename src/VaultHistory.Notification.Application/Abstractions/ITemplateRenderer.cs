using VaultHistory.Notification.Domain.Abstractions;

namespace VaultHistory.Notification.Application.Abstractions;

public interface ITemplateRenderer
{
    Task<Result> RenderAsync(string templateName, object model, CancellationToken cancellationToken);
}
