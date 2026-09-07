using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Infrastructure.History;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.IntegrationTests;

public sealed class HistoryClientTests
{
    [Fact]
    public async Task Sends_the_subscription_contract_and_reads_a_created_history()
    {
        HttpRequestMessage? sent = null;
        var client = CreateClient(async request =>
        {
            sent = await CloneAsync(request);
            return JsonResponse(HttpStatusCode.Created, "{\"history\":\"A generated story\"}");
        });

        var result = await client.GenerateSubscriptionAsync(new("user-123", new DateTimeOffset(1999, 12, 31, 0, 0, 0, TimeSpan.Zero), "fantasy", "mage"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("A generated story", result.Value);
        Assert.NotNull(sent);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("/api/v1/history/generate/subscription", sent.RequestUri?.AbsolutePath);
        Assert.Equal("job-token", sent.Headers.GetValues("Authorization").Single());
        Assert.Equal("{\"userId\":\"user-123\",\"date\":\"1999-12-31\",\"theme\":\"fantasy\",\"character\":\"mage\"}", await sent.Content!.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "history.bad_request")]
    [InlineData(HttpStatusCode.Unauthorized, "history.unauthorized")]
    [InlineData(HttpStatusCode.TooManyRequests, "history.rate_limited")]
    [InlineData(HttpStatusCode.InternalServerError, "history.server_error")]
    public async Task Classifies_non_success_responses(HttpStatusCode statusCode, string expectedCode)
    {
        var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(statusCode)));

        var result = await client.GenerateSubscriptionAsync(new("user-123", null, null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error?.Code);
    }

    [Fact]
    public async Task Omits_optional_values_from_the_request_body()
    {
        HttpRequestMessage? sent = null;
        var client = CreateClient(async request =>
        {
            sent = await CloneAsync(request);
            return JsonResponse(HttpStatusCode.Created, "{\"history\":\"A story\"}");
        });

        await client.GenerateSubscriptionAsync(new("user-123", null, null, null), CancellationToken.None);

        Assert.Equal("{\"userId\":\"user-123\",\"date\":null,\"theme\":null,\"character\":null}", await sent!.Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task Classifies_invalid_json_and_empty_history()
    {
        var invalidJsonClient = CreateClient(_ => Task.FromResult(JsonResponse(HttpStatusCode.Created, "{not-json")));
        var emptyHistoryClient = CreateClient(_ => Task.FromResult(JsonResponse(HttpStatusCode.Created, "{\"history\":\" \"}")));

        var invalidJson = await invalidJsonClient.GenerateSubscriptionAsync(new("user-123", null, null, null), CancellationToken.None);
        var emptyHistory = await emptyHistoryClient.GenerateSubscriptionAsync(new("user-123", null, null, null), CancellationToken.None);

        Assert.Equal("history.invalid_response", invalidJson.Error?.Code);
        Assert.Equal("history.empty_response", emptyHistory.Error?.Code);
    }

    [Fact]
    public async Task Classifies_request_timeout_without_retrying_the_post()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException());
        var client = CreateClient(handler);

        var result = await client.GenerateSubscriptionAsync(new("user-123", null, null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("history.timeout", result.Error?.Code);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Propagates_caller_cancellation()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => throw new TaskCanceledException()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GenerateSubscriptionAsync(new("user-123", null, null, null), cancellation.Token));
    }

    private static HistoryClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) =>
        CreateClient(new StubHttpMessageHandler(response));

    private static HistoryClient CreateClient(StubHttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://history.example.test/") },
            Microsoft.Extensions.Options.Options.Create(new HistoryOptions
            {
                BaseUrl = "https://history.example.test/",
                SubscriptionPath = "api/v1/history/generate/subscription",
                AuthorizationToken = "job-token",
                TimeoutSeconds = 30
            }),
            NullLogger<HistoryClient>.Instance);

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string content) =>
        new(statusCode) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        clone.Content = new StringContent(await request.Content!.ReadAsStringAsync(), Encoding.UTF8, request.Content.Headers.ContentType?.MediaType);
        return clone;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return handler(request);
        }
    }
}
