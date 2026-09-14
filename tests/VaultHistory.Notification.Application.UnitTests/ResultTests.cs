using VaultHistory.Notification.Domain.Abstractions;

namespace VaultHistory.Notification.Application.UnitTests;

public sealed class ResultTests
{
    [Fact]
    public void Failure_preserves_the_error()
    {
        var error = new Error("notification.failed", "The notification could not be sent.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }
}
