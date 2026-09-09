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

    private static readonly NotifyOutboxMessage SignInMessage = new(
        "outbox-signin-123",
        "user-123",
        "ana@example.test",
        "Ana Pérez",
        null,
        "UserSignedInEvent",
        new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));

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

    [Fact]
    public async Task Reuses_persisted_stages_when_a_history_message_is_redelivered()
    {
        var history = new RecordingHistoryClient(Result<string>.Success("Story that must not be regenerated"));
        var sender = new RecordingEmailSender(Result.Success());
        var checkpointStore = new InMemoryCheckpointStore();
        var workflow = new NotificationWorkflow(
            history,
            new RecordingTemplateRenderer(Result<string>.Success("<p>Story</p>")),
            sender,
            new RecordingResultPublisher(Result.Failure(new Error("kafka.unavailable", "Temporary failure."))),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 30, 0, TimeSpan.Zero)),
            checkpointStore);
        var message = Message with { NotificationId = "user-123:2026" };

        var first = await workflow.HandleHistoryAsync(message, CancellationToken.None);
        var second = await workflow.HandleHistoryAsync(message, CancellationToken.None);

        Assert.False(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(1, history.CallCount);
        Assert.Equal(1, sender.CallCount);
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
        Assert.Equal(errorCode == "history.unavailable" ? "PENDING" : "ERROR", notification.NotificationStatus);
        Assert.Null(notification.NotificationDate);
        Assert.Equal(errorCode.ToUpperInvariant().Replace('.', '_'), notification.FailureStage);
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

    [Fact]
    public async Task Renders_sign_in_sends_email_and_processes_the_matching_outbox()
    {
        var history = new RecordingHistoryClient(Result<string>.Success("Story"));
        var renderer = new RecordingTemplateRenderer(Result<string>.Success("<h1>Sign in</h1>"));
        var sender = new RecordingEmailSender(Result.Success());
        var publisher = new RecordingResultPublisher(Result.Success());
        var workflow = CreateWorkflow(history, renderer, sender, publisher);

        var result = await workflow.HandleOutboxAsync(SignInMessage, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, history.CallCount);
        Assert.Equal(EmailTemplate.SignIn, renderer.Template);
        var model = Assert.IsType<SignInEmailTemplateModel>(renderer.Model);
        Assert.Equal(SignInMessage.OccurredOn, model.OccurredOn);
        Assert.Equal("Nuevo inicio de sesión en Vault History", sender.Subject);
        var outbox = Assert.Single(publisher.OutboxResults);
        Assert.Equal("outbox-signin-123", outbox.OutboxId);
        Assert.Equal("PROCESSED", outbox.Status);
        Assert.Null(outbox.Error);
    }

    [Fact]
    public async Task Uses_welcome_template_for_create_user_event()
    {
        var renderer = new RecordingTemplateRenderer(Result<string>.Success("<h1>Welcome</h1>"));
        var sender = new RecordingEmailSender(Result.Success());
        var publisher = new RecordingResultPublisher(Result.Success());
        var workflow = CreateWorkflow(new RecordingHistoryClient(Result<string>.Success("Story")), renderer, sender, publisher);
        var message = SignInMessage with { OutboxId = "outbox-welcome-123", Type = "CreateUserEvent", OccurredOn = null };

        var result = await workflow.HandleOutboxAsync(message, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailTemplate.Welcome, renderer.Template);
        Assert.IsType<WelcomeEmailTemplateModel>(renderer.Model);
        Assert.Equal("Bienvenido a Vault History", sender.Subject);
        Assert.Equal("outbox-welcome-123", Assert.Single(publisher.OutboxResults).OutboxId);
    }

    [Theory]
    [InlineData("templates.invalid_template", "template")]
    [InlineData("gmail.rejected", "email")]
    public async Task Publishes_a_sanitized_outbox_error_when_rendering_or_delivery_fails(string errorCode, string failedStage)
    {
        var renderer = new RecordingTemplateRenderer(failedStage == "template"
            ? Result<string>.Failure(new Error(errorCode, "secret detail"))
            : Result<string>.Success("<p>Sign in</p>"));
        var sender = new RecordingEmailSender(failedStage == "email"
            ? Result.Failure(new Error(errorCode, "secret detail"))
            : Result.Success());
        var publisher = new RecordingResultPublisher(Result.Success());
        var workflow = CreateWorkflow(new RecordingHistoryClient(Result<string>.Success("Story")), renderer, sender, publisher);

        var result = await workflow.HandleOutboxAsync(SignInMessage, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(failedStage == "template" ? 0 : 1, sender.CallCount);
        var outbox = Assert.Single(publisher.OutboxResults);
        Assert.Equal("outbox-signin-123", outbox.OutboxId);
        Assert.Equal("ERROR", outbox.Status);
        Assert.Equal(errorCode.ToUpperInvariant().Replace('.', '_'), outbox.Error);
        Assert.DoesNotContain("secret", outbox.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Correlates_multiple_sign_ins_for_the_same_user_to_their_own_outbox_ids()
    {
        var publisher = new RecordingResultPublisher(Result.Success());
        var workflow = CreateWorkflow(
            new RecordingHistoryClient(Result<string>.Success("Story")),
            new RecordingTemplateRenderer(Result<string>.Success("<p>Sign in</p>")),
            new RecordingEmailSender(Result.Success()),
            publisher);

        await workflow.HandleOutboxAsync(SignInMessage, CancellationToken.None);
        await workflow.HandleOutboxAsync(SignInMessage with { OutboxId = "outbox-signin-456" }, CancellationToken.None);

        Assert.Equal(["outbox-signin-123", "outbox-signin-456"], publisher.OutboxResults.Select(result => result.OutboxId));
    }

    [Fact]
    public async Task Rejects_an_unknown_outbox_type_without_rendering_or_sending()
    {
        var renderer = new RecordingTemplateRenderer(Result<string>.Success("html"));
        var sender = new RecordingEmailSender(Result.Success());
        var publisher = new RecordingResultPublisher(Result.Success());
        var workflow = CreateWorkflow(new RecordingHistoryClient(Result<string>.Success("Story")), renderer, sender, publisher);

        var result = await workflow.HandleOutboxAsync(SignInMessage with { Type = "PasswordResetEvent" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, renderer.CallCount);
        Assert.Equal(0, sender.CallCount);
        Assert.Equal("NOTIFICATION_UNSUPPORTED_OUTBOX_TYPE", Assert.Single(publisher.OutboxResults).Error);
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
        public List<OutboxNotificationResult> OutboxResults { get; } = [];

        public Task<Result> PublishUserResultAsync(UserNotificationResult notificationResult, CancellationToken cancellationToken)
        {
            UserResults.Add(notificationResult);
            return Task.FromResult(result);
        }

        public Task<Result> PublishOutboxResultAsync(OutboxNotificationResult notificationResult, CancellationToken cancellationToken)
        {
            OutboxResults.Add(notificationResult);
            return Task.FromResult(result);
        }
    }

    private sealed class InMemoryCheckpointStore : INotificationCheckpointStore
    {
        private readonly Dictionary<string, NotificationCheckpoint> _checkpoints = [];

        public Task<NotificationCheckpoint?> GetAsync(string notificationId, CancellationToken cancellationToken) =>
            Task.FromResult(_checkpoints.GetValueOrDefault(notificationId));

        public Task SaveAsync(NotificationCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            _checkpoints[checkpoint.NotificationId] = checkpoint;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
