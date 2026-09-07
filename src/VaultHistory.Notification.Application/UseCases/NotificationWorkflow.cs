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
    TimeProvider timeProvider) : INotificationWorkflow
{
    private static readonly Error OutboxWorkflowNotReady = new(
        "notification.workflow_not_ready",
        "Outbox notification workflows are introduced in HU-08.");

    public async Task<Result> HandleHistoryAsync(NotifyHistoryMessage message, CancellationToken cancellationToken)
    {
        var validationError = Validate(message);
        if (validationError is not null)
        {
            return string.IsNullOrWhiteSpace(message.UserId)
                ? Result.Failure(validationError)
                : await PublishErrorAsync(message.UserId, cancellationToken);
        }

        var history = await GenerateHistoryAsync(message, cancellationToken);
        if (!history.IsSuccess)
        {
            return await PublishErrorAsync(message.UserId, cancellationToken);
        }

        var template = await RenderHistoryAsync(message, history.Value!, cancellationToken);
        if (!template.IsSuccess)
        {
            return await PublishErrorAsync(message.UserId, cancellationToken);
        }

        var email = await SendEmailAsync(message.Email, template.Value!, cancellationToken);
        if (!email.IsSuccess)
        {
            return await PublishErrorAsync(message.UserId, cancellationToken);
        }

        return await resultPublisher.PublishUserResultAsync(
            new UserNotificationResult(message.UserId, "NOTIFIED", timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public Task<Result> HandleOutboxAsync(NotifyOutboxMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(OutboxWorkflowNotReady));

    private async Task<Result> PublishErrorAsync(string userId, CancellationToken cancellationToken) =>
        await resultPublisher.PublishUserResultAsync(
            new UserNotificationResult(userId, "ERROR", null),
            cancellationToken);

    private async Task<Result<string>> GenerateHistoryAsync(NotifyHistoryMessage message, CancellationToken cancellationToken)
    {
        try
        {
            return await historyClient.GenerateSubscriptionAsync(
                new GenerateSubscriptionHistoryRequest(message.UserId, message.BirthDate, message.Theme, message.Character),
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

    private async Task<Result> SendEmailAsync(string email, string html, CancellationToken cancellationToken)
    {
        try
        {
            return await emailSender.SendAsync(email, "Tu historia de Vault History", html, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(new Error("gmail.unexpected_failure", exception.Message));
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
}
