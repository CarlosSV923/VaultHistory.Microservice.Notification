using System.Net.Mail;
using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Domain.Notifications;

namespace VaultHistory.Notification.Application.UseCases;

public sealed class NotificationWorkflow(
    IHistoryClient historyClient,
    ITemplateRenderer templateRenderer,
    IEmailSender emailSender,
    INotificationResultPublisher resultPublisher,
    TimeProvider timeProvider,
    INotificationCheckpointStore? checkpointStore = null) : INotificationWorkflow
{
    public async Task<Result> HandleHistoryAsync(NotifyHistoryMessage message, CancellationToken cancellationToken)
    {
        var validationError = Validate(message);
        if (validationError is not null)
        {
            return string.IsNullOrWhiteSpace(message.UserId)
                ? Result.Failure(validationError)
                : await PublishErrorAsync(message.UserId, cancellationToken);
        }

        var notificationId = string.IsNullOrWhiteSpace(message.NotificationId) ? message.UserId : message.NotificationId;
        NotificationCheckpoint? checkpoint;
        try { checkpoint = checkpointStore is null ? null : await checkpointStore.GetAsync(notificationId, cancellationToken); }
        catch (Exception exception) { return Result.Failure(new Error("checkpoints.unavailable", exception.Message)); }
        if (checkpoint?.ResultPublishedAt is not null) return Result.Success();

        var history = checkpoint?.Story is { Length: > 0 }
            ? Result<string>.Success(checkpoint.Story)
            : await GenerateHistoryAsync(message, cancellationToken);
        if (!history.IsSuccess) return await PublishErrorAsync(message.UserId, cancellationToken);
        checkpoint ??= new NotificationCheckpoint(notificationId, message.UserId, null, null, null);
        if (checkpoint.Story is null && checkpointStore is not null)
        {
            checkpoint = checkpoint with { Story = history.Value };
            var saved = await SaveCheckpointAsync(checkpoint, cancellationToken);
            if (!saved.IsSuccess) return saved;
        }

        var template = await RenderHistoryAsync(message, history.Value!, cancellationToken);
        if (!template.IsSuccess)
        {
            return await PublishErrorAsync(message.UserId, cancellationToken);
        }

        var email = checkpoint.EmailSentAt is null
            ? await SendEmailAsync(message.Email, "Tu historia de Vault History", template.Value!, cancellationToken)
            : Result.Success();
        if (!email.IsSuccess)
        {
            return await PublishErrorAsync(message.UserId, cancellationToken);
        }

        if (checkpoint.EmailSentAt is null && checkpointStore is not null)
        {
            checkpoint = checkpoint with { EmailSentAt = timeProvider.GetUtcNow() };
            var saved = await SaveCheckpointAsync(checkpoint, cancellationToken);
            if (!saved.IsSuccess) return saved;
        }
        var published = await resultPublisher.PublishUserResultAsync(
            new UserNotificationResult(message.UserId, "NOTIFIED", timeProvider.GetUtcNow()),
            cancellationToken);
        if (published.IsSuccess && checkpointStore is not null)
        {
            var saved = await SaveCheckpointAsync(checkpoint with { ResultPublishedAt = timeProvider.GetUtcNow() }, cancellationToken);
            if (!saved.IsSuccess) return saved;
        }
        return published;
    }

    public async Task<Result> HandleOutboxAsync(NotifyOutboxMessage message, CancellationToken cancellationToken)
    {
        var validationError = Validate(message);
        if (validationError is not null)
        {
            return string.IsNullOrWhiteSpace(message.OutboxId)
                ? Result.Failure(validationError)
                : await PublishOutboxErrorAsync(message.OutboxId, validationError, cancellationToken);
        }

        var selection = SelectOutboxTemplate(message);
        if (!selection.IsSuccess)
        {
            return await PublishOutboxErrorAsync(message.OutboxId, selection.Error!, cancellationToken);
        }

        var template = await RenderOutboxAsync(selection.Value!, message, cancellationToken);
        if (!template.IsSuccess)
        {
            return await PublishOutboxErrorAsync(message.OutboxId, template.Error!, cancellationToken);
        }

        var email = await SendEmailAsync(message.Email, selection.Value!.Subject, template.Value!, cancellationToken);
        if (!email.IsSuccess)
        {
            return await PublishOutboxErrorAsync(message.OutboxId, email.Error!, cancellationToken);
        }

        return await resultPublisher.PublishOutboxResultAsync(
            new OutboxNotificationResult(message.OutboxId, "PROCESSED", null),
            cancellationToken);
    }

    private async Task<Result> PublishErrorAsync(string userId, CancellationToken cancellationToken) =>
        await resultPublisher.PublishUserResultAsync(
            new UserNotificationResult(userId, "ERROR", null),
            cancellationToken);

    private async Task<Result> PublishOutboxErrorAsync(string outboxId, Error error, CancellationToken cancellationToken) =>
        await resultPublisher.PublishOutboxResultAsync(
            new OutboxNotificationResult(outboxId, "ERROR", ToSafeErrorCode(error.Code)),
            cancellationToken);

    private async Task<Result<string>> GenerateHistoryAsync(NotifyHistoryMessage message, CancellationToken cancellationToken)
    {
        try
        {
            return await historyClient.GenerateSubscriptionAsync(
                new GenerateSubscriptionHistoryRequest(message.UserId, message.BirthDate, message.Theme, message.Character, message.NotificationId),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure(new Error("history.unexpected_failure", exception.Message));
        }
    }

    private async Task<Result<string>> RenderHistoryAsync(NotifyHistoryMessage message, string story, CancellationToken cancellationToken)
    {
        try
        {
            return await templateRenderer.RenderAsync(
                EmailTemplate.History,
                new HistoryEmailTemplateModel(message.Fullname, message.BirthDate, story),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure(new Error("templates.unexpected_failure", exception.Message));
        }
    }

    private async Task<Result<string>> RenderOutboxAsync(OutboxTemplateSelection selection, NotifyOutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            return await templateRenderer.RenderAsync(selection.Template, selection.CreateModel(message), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure(new Error("templates.unexpected_failure", exception.Message));
        }
    }

    private async Task<Result> SendEmailAsync(string email, string subject, string html, CancellationToken cancellationToken)
    {
        try
        {
            return await emailSender.SendAsync(email, subject, html, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(new Error("gmail.unexpected_failure", exception.Message));
        }
    }

    private async Task<Result> SaveCheckpointAsync(NotificationCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        try
        {
            await checkpointStore!.SaveAsync(checkpoint, cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(new Error("checkpoints.unavailable", exception.Message));
        }
    }

    private static Error? Validate(NotifyHistoryMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.UserId))
        {
            return new Error("notification.invalid_message", "A history notification requires a user ID.");
        }

        if (string.IsNullOrWhiteSpace(message.Fullname))
        {
            return new Error("notification.invalid_message", "A history notification requires the recipient full name.");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(message.Email) || new MailAddress(message.Email).Address != message.Email)
            {
                return new Error("notification.invalid_message", "A history notification requires a valid recipient email.");
            }
        }
        catch (FormatException)
        {
            return new Error("notification.invalid_message", "A history notification requires a valid recipient email.");
        }

        return null;
    }

    private static Error? Validate(NotifyOutboxMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.OutboxId))
        {
            return new Error("notification.invalid_message", "An outbox notification requires an outbox ID.");
        }

        if (string.IsNullOrWhiteSpace(message.Fullname))
        {
            return new Error("notification.invalid_message", "An outbox notification requires the recipient full name.");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(message.Email) || new MailAddress(message.Email).Address != message.Email)
            {
                return new Error("notification.invalid_message", "An outbox notification requires a valid recipient email.");
            }
        }
        catch (FormatException)
        {
            return new Error("notification.invalid_message", "An outbox notification requires a valid recipient email.");
        }

        return null;
    }

    private static Result<OutboxTemplateSelection> SelectOutboxTemplate(NotifyOutboxMessage message)
    {
        if (string.Equals(message.Type, "UserSignedInEvent", StringComparison.Ordinal))
        {
            return message.OccurredOn is null
                ? Result<OutboxTemplateSelection>.Failure(new Error("notification.invalid_message", "A sign-in notification requires its occurrence time."))
                : Result<OutboxTemplateSelection>.Success(new OutboxTemplateSelection(
                    EmailTemplate.SignIn,
                    "Nuevo inicio de sesión en Vault History",
                    source => new SignInEmailTemplateModel(source.Fullname, source.OccurredOn!.Value)));
        }

        if (string.Equals(message.Type, "CreateUserEvent", StringComparison.Ordinal))
        {
            return Result<OutboxTemplateSelection>.Success(new OutboxTemplateSelection(
                EmailTemplate.Welcome,
                "Bienvenido a Vault History",
                source => new WelcomeEmailTemplateModel(source.Fullname)));
        }

        return Result<OutboxTemplateSelection>.Failure(new Error("notification.unsupported_outbox_type", "The outbox event type is not supported."));
    }

    private static string ToSafeErrorCode(string code)
    {
        var safeCode = new string(code.ToUpperInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '_').ToArray()).Trim('_');

        return string.IsNullOrEmpty(safeCode) ? "NOTIFICATION_FAILED" : safeCode[..Math.Min(safeCode.Length, 80)];
    }

    private sealed record OutboxTemplateSelection(
        EmailTemplate Template,
        string Subject,
        Func<NotifyOutboxMessage, EmailTemplateModel> CreateModel);
}
