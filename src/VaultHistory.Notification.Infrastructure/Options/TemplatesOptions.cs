using System.ComponentModel.DataAnnotations;

namespace VaultHistory.Notification.Infrastructure.Options;

public sealed class TemplatesOptions
{
    public const string SectionName = "Templates";

    [Required] public string HistoryTemplatePath { get; init; } = string.Empty;
    [Required] public string SignInTemplatePath { get; init; } = string.Empty;
    [Required] public string WelcomeTemplatePath { get; init; } = string.Empty;
}
