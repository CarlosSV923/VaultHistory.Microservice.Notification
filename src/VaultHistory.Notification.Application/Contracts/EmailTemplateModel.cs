namespace VaultHistory.Notification.Application.Contracts;

public abstract record EmailTemplateModel(string Fullname);

public sealed record HistoryEmailTemplateModel(
    string Fullname,
    DateTimeOffset? BirthDate,
    string Story) : EmailTemplateModel(Fullname);

public sealed record SignInEmailTemplateModel(
    string Fullname,
    DateTimeOffset OccurredOn) : EmailTemplateModel(Fullname);

public sealed record WelcomeEmailTemplateModel(
    string Fullname) : EmailTemplateModel(Fullname);
