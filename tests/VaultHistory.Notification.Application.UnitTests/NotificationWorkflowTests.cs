using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Application.UseCases;
using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Domain.Notifications;

namespace VaultHistory.Notification.Application.UnitTests;

public sealed class NotificationWorkflowTests
{
    private static readonly NotifyHistoryMessage Message = new(
        "user-123",
        "ana@example.test",
        "Ana Pérez",
        new DateTimeOffset(1999, 12, 31, 0, 0, 0, TimeSpan.Zero),
        "adventure",
        "mage");

    [Fact]
    public async Task Generates_renders_sends_and_confirms_a_history_notification()
    {
        var history = new RecordingHistoryClient(Result<string>.Success("Una historia generada."));
        var renderer = new RecordingTemplateRenderer(Result<string>.Success("<h1>Historia</h1>"));
        var sender = new RecordingEmailSender(Result.Success());
        var publisher = new RecordingResultPublisher(Result.Success());
        var workflow = CreateWorkflow(history, renderer, sender, publisher);

        var result = await workflow.HandleHistoryAsync(Message, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new GenerateSubscriptionHistoryRequest("user-123", Message.BirthDate, "adventure", "mage"), history.Request);
        Assert.Equal(EmailTemplate.History, renderer.Template);
        var model = Assert.IsType<HistoryEmailTemplateModel>(renderer.Model);
        Assert.Equal("Ana Pérez", model.Fullname);
        Assert.Equal("Una historia generada.", model.Story);
        Assert.Equal("ana@example.test", sender.Recipient);
        Assert.Equal("Tu historia de Vault History", sender.Subject);
        Assert.Equal("<h1>Historia</h1>", sender.Html);
        var notification = Assert.Single(publisher.UserResults);
        Assert.Equal("user-123", notification.UserId);
        Assert.Equal("NOTIFIED", notification.NotificationStatus);
        Assert.Equal(new DateTimeOffset(2026, 9, 7, 12, 30, 0, TimeSpan.Zero), notification.NotificationDate);
    }

    [Theory]
    [InlineData("history.unavailable", "history")]
    [InlineData("templates.invalid_template", "template")]
    [InlineData("gmail.rejected", "email")]
    public async Task Publishes_error_without_continuing_after_a_failed_stage(string errorCode, string failedStage)
    {
        var history = new RecordingHistoryClient(failedStage == "history"
            ? Result<string>.Failure(new Error(errorCode, "Failure."))
            : Result<string>.Success("Story"));
        var renderer = new RecordingTemplateRenderer(failedStage == "template"
            ? Result<string>.Failure(new Error(errorCode, "Failure."))
            : Result<string>.Success("<p>Story</p>"));
        var sender = new RecordingEmailSender(failedStage == "email"
            ? Result.Failure(new Error(errorCode, "Failure."))
            : Result.Success());
        var publisher = new RecordingResultPublisher(Result.Success());
        var workflow = CreateWorkflow(history, renderer, sender, publisher);

        var result = await workflow.HandleHistoryAsync(Message, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, history.CallCount);
        Assert.Equal(failedStage == "history" ? 0 : 1, renderer.CallCount);
        Assert.Equal(failedStage is "history" or "template" ? 0 : 1, sender.CallCount);
        var notification = Assert.Single(publisher.UserResults);
        Assert.Equal("ERROR", notification.NotificationStatus);
        Assert.Null(notification.NotificationDate);
    }

    [Fact]
    public async Task Returns_publication_failure_after_email_without_repeating_completed_stages()
    {
        var history = new RecordingHistoryClient(Result<string>.Success("Story"));
        var renderer = new RecordingTemplateRenderer(Result<string>.Success("<p>Story</p>"));
        var sender = new RecordingEmailSender(Result.Success());
        var publisher = new RecordingResultPublisher(Result.Failure(new Error("kafka.publish_failed", "Broker unavailable.")));
        var workflow = CreateWorkflow(history, renderer, sender, publisher);

        var result = await workflow.HandleHistoryAsync(Message, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("kafka.publish_failed", result.Error?.Code);
        Assert.Equal(1, history.CallCount);
        Assert.Equal(1, renderer.CallCount);
        Assert.Equal(1, sender.CallCount);
        Assert.Single(publisher.UserResults);
    }

    [Fact]
    public async Task Publishes_error_without_calling_history_when_message_is_invalid()
    {
        var history = new RecordingHistoryClient(Result<string>.Success("Story"));
        var publisher = new RecordingResultPublisher(Result.Success());
        var workflow = CreateWorkflow(history, new RecordingTemplateRenderer(Result<string>.Success("html")), new RecordingEmailSender(Result.Success()), publisher);

        var result = await workflow.HandleHistoryAsync(Message with { Email = "not-an-email" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, history.CallCount);
        var notification = Assert.Single(publisher.UserResults);
        Assert.Equal("ERROR", notification.NotificationStatus);
    }

    private static NotificationWorkflow CreateWorkflow(
        RecordingHistoryClient history,
        RecordingTemplateRenderer renderer,
        RecordingEmailSender sender,
        RecordingResultPublisher publisher) =>
        new(history, renderer, sender, publisher, new FixedTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 30, 0, TimeSpan.Zero)));

    private sealed class RecordingHistoryClient(Result<string> result) : IHistoryClient
    {
        public GenerateSubscriptionHistoryRequest? Request { get; private set; }
        public int CallCount { get; private set; }

        public Task<Result<string>> GenerateSubscriptionAsync(GenerateSubscriptionHistoryRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingTemplateRenderer(Result<string> result) : ITemplateRenderer
    {
        public EmailTemplate? Template { get; private set; }
        public EmailTemplateModel? Model { get; private set; }
        public int CallCount { get; private set; }

        public Task<Result<string>> RenderAsync(EmailTemplate template, EmailTemplateModel model, CancellationToken cancellationToken)
        {
            CallCount++;
            Template = template;
            Model = model;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingEmailSender(Result result) : IEmailSender
    {
        public string? Recipient { get; private set; }
        public string? Subject { get; private set; }
        public string? Html { get; private set; }
        public int CallCount { get; private set; }

        public Task<Result> SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken)
        {
            CallCount++;
            Recipient = recipient;
            Subject = subject;
            Html = htmlBody;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingResultPublisher(Result result) : INotificationResultPublisher
    {
        public List<UserNotificationResult> UserResults { get; } = [];

        public Task<Result> PublishUserResultAsync(UserNotificationResult notificationResult, CancellationToken cancellationToken)
        {
            UserResults.Add(notificationResult);
            return Task.FromResult(result);
        }

        public Task<Result> PublishOutboxResultAsync(OutboxNotificationResult notificationResult, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
