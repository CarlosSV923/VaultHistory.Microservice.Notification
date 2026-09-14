using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using VaultHistory.Notification.Infrastructure.Gmail;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.IntegrationTests;

public sealed class GmailEmailSenderTests
{
    [Fact]
    public async Task Builds_a_utf8_html_mime_message_and_sends_its_base64url_representation()
    {
        var gmail = new RecordingGmailClient();
        var sender = CreateSender(gmail);

        var result = await sender.SendAsync("ana@example.test", "Bienvenida, Ana", "<h1>Hola</h1>", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var mime = Decode(gmail.RawMessage!);
        Assert.Contains("vault-history@example.test", mime);
        Assert.Contains("To: ana@example.test", mime);
        Assert.Contains("Subject: =?utf-8?B?QmllbnZlbmlkYSwgQW5h?=", mime);
        Assert.Contains("Content-Type: text/html; charset=utf-8", mime);
        Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("<h1>Hola</h1>")), mime);
        Assert.DoesNotContain("+", gmail.RawMessage);
        Assert.DoesNotContain("/", gmail.RawMessage);
        Assert.DoesNotContain("=", gmail.RawMessage);
    }

    [Theory]
    [InlineData("gmail.authentication_failed")]
    [InlineData("gmail.quota_exceeded")]
    [InlineData("gmail.rejected")]
    public async Task Returns_controlled_gmail_failures_without_sending_real_email(string code)
    {
        var sender = CreateSender(new RecordingGmailClient(new GmailDeliveryException(code, "Delivery denied.")));

        var result = await sender.SendAsync("ana@example.test", "Subject", "<p>Body</p>", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error?.Code);
    }

    [Fact]
    public async Task Returns_timeout_when_the_gmail_client_does_not_complete()
    {
        var sender = CreateSender(new RecordingGmailClient(new TaskCanceledException()));

        var result = await sender.SendAsync("ana@example.test", "Subject", "<p>Body</p>", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("gmail.timeout", result.Error?.Code);
    }

    [Fact]
    public async Task Rejects_an_invalid_recipient_before_calling_gmail()
    {
        var gmail = new RecordingGmailClient();
        var sender = CreateSender(gmail);

        var result = await sender.SendAsync("not an email", "Subject", "<p>Body</p>", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("gmail.invalid_recipient", result.Error?.Code);
        Assert.Null(gmail.RawMessage);
    }

    [Fact]
    public async Task Propagates_caller_cancellation()
    {
        var sender = CreateSender(new RecordingGmailClient(new TaskCanceledException()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sender.SendAsync("ana@example.test", "Subject", "<p>Body</p>", cancellation.Token));
    }

    private static GmailEmailSender CreateSender(RecordingGmailClient gmail) =>
        new(
            gmail,
            Microsoft.Extensions.Options.Options.Create(new GmailOptions
            {
                SenderAddress = "vault-history@example.test",
                SenderName = "Vault History",
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RefreshToken = "refresh-token"
            }),
            NullLogger<GmailEmailSender>.Instance);

    private static string Decode(string raw) => Encoding.UTF8.GetString(Convert.FromBase64String(raw.Replace('-', '+').Replace('_', '/') + new string('=', (4 - raw.Length % 4) % 4)));

    private sealed class RecordingGmailClient(Exception? exception = null) : IGmailMessageClient
    {
        public string? RawMessage { get; private set; }

        public Task<string> SendRawAsync(string rawMessage, CancellationToken cancellationToken)
        {
            RawMessage = rawMessage;
            return exception is null ? Task.FromResult("gmail-message-123") : Task.FromException<string>(exception);
        }
    }
}
