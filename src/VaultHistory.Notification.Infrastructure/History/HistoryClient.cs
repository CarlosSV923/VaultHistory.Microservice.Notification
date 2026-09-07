using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.History;

public sealed class HistoryClient(
    HttpClient httpClient,
    IOptions<HistoryOptions> options,
    ILogger<HistoryClient> logger) : IHistoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<string>> GenerateSubscriptionAsync(GenerateSubscriptionHistoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);

        var settings = options.Value;
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.SubscriptionPath)
        {
            Content = JsonContent.Create(
                new GenerateSubscriptionHistoryBody(
                    request.UserId,
                    request.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    request.Theme,
                    request.Character),
                options: JsonOptions)
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", settings.AuthorizationToken);

        try
        {
            using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<string>.Failure(ToHttpError(response.StatusCode));
            }

            var body = await response.Content.ReadFromJsonAsync<GenerateSubscriptionHistoryResponse>(JsonOptions, cancellationToken);
            if (string.IsNullOrWhiteSpace(body?.History))
            {
                return Result<string>.Failure(new Error("history.empty_response", "History returned an empty story."));
            }

            return Result<string>.Success(body.History);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure(new Error("history.timeout", "History request timed out."));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "History service request failed.");
            return Result<string>.Failure(new Error("history.unavailable", "History service is unavailable."));
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "History service returned invalid JSON.");
            return Result<string>.Failure(new Error("history.invalid_response", "History returned an invalid response."));
        }
    }

    private static Error ToHttpError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => new Error("history.bad_request", "History rejected the request."),
        HttpStatusCode.Unauthorized => new Error("history.unauthorized", "History rejected the configured authorization token."),
        HttpStatusCode.TooManyRequests => new Error("history.rate_limited", "History rate limit was reached."),
        >= HttpStatusCode.InternalServerError => new Error("history.server_error", "History service failed while generating the story."),
        _ => new Error("history.http_error", $"History returned HTTP {(int)statusCode}.")
    };

    private sealed record GenerateSubscriptionHistoryBody(string UserId, string? Date, string? Theme, string? Character);

    private sealed record GenerateSubscriptionHistoryResponse(string? History);
}
