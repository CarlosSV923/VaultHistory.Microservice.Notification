using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.Gmail;

public sealed class GmailApiMessageClient(IOptions<GmailOptions> options) : IGmailMessageClient
{
    public async Task<string> SendRawAsync(string rawMessage, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var credential = CreateCredential(settings);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

            if (!await credential.RefreshTokenAsync(timeout.Token))
            {
                throw new GmailDeliveryException("gmail.authentication_failed", "Gmail did not accept the configured refresh token.");
            }

            using var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Vault History Notification"
            });
            var request = service.Users.Messages.Send(new Google.Apis.Gmail.v1.Data.Message { Raw = rawMessage }, "me");
            var response = await request.ExecuteAsync(timeout.Token);

            return response.Id ?? throw new GmailDeliveryException("gmail.invalid_response", "Gmail accepted the request without returning a message identifier.");
        }
        catch (TokenResponseException exception)
        {
            throw new GmailDeliveryException("gmail.authentication_failed", "Gmail rejected the configured OAuth credentials.", exception);
        }
        catch (GoogleApiException exception)
        {
            throw ToDeliveryException(exception);
        }
    }

    private static UserCredential CreateCredential(GmailOptions settings)
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = settings.ClientId,
                ClientSecret = settings.ClientSecret
            },
            Scopes = [GmailService.Scope.GmailSend],
            DataStore = new NullDataStore()
        });

        return new UserCredential(flow, settings.SenderAddress, new TokenResponse { RefreshToken = settings.RefreshToken });
    }

    private static GmailDeliveryException ToDeliveryException(GoogleApiException exception)
    {
        var code = exception.HttpStatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "gmail.authentication_failed",
            System.Net.HttpStatusCode.TooManyRequests => "gmail.quota_exceeded",
            System.Net.HttpStatusCode.Forbidden => "gmail.rejected",
            _ => "gmail.send_failed"
        };

        return new GmailDeliveryException(code, "Gmail rejected the email delivery request.", exception);
    }
}
