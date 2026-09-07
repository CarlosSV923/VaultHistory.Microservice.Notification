# Gmail OAuth configuration

The worker sends through Gmail API with the minimum `https://www.googleapis.com/auth/gmail.send` scope. It never opens an OAuth browser or persists a token while it runs; a refresh token must be generated once by the account owner and injected as a secret.

Configure these environment variables or .NET user secrets:

```text
Gmail__SenderAddress
Gmail__SenderName
Gmail__ClientId
Gmail__ClientSecret
Gmail__RefreshToken
```

Enable Gmail API in the selected Google Cloud project, configure the consent screen, add the sender as a test user while the application is in Testing, and complete the authorization-code flow with offline access. A Google external application in Testing can issue refresh tokens that expire; review the publishing and verification requirements before relying on unattended delivery.

Keep the refresh token and client secret out of `appsettings.json`, Docker images, Git history and logs. Revoke the account's OAuth grant if either secret may have been exposed. Gmail acceptance means Gmail accepted the API request; it does not guarantee delivery to the recipient inbox.
