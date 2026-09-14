using Microsoft.Extensions.Configuration;
using Npgsql;
using VaultHistory.Notification.Application.Abstractions;

namespace VaultHistory.Notification.Infrastructure.Checkpoints;

public sealed class PostgresNotificationCheckpointStore(IConfiguration configuration) : INotificationCheckpointStore
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for notification checkpoints.");

    public async Task<NotificationCheckpoint?> GetAsync(string notificationId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select notification_id, user_id, story, email_sent_at, result_published_at from notification_checkpoints where notification_id = @id", connection);
        command.Parameters.AddWithValue("id", notificationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new NotificationCheckpoint(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    public async Task SaveAsync(NotificationCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("insert into notification_checkpoints(notification_id,user_id,story,email_sent_at,result_published_at,updated_at) values(@id,@user,@story,@email,@result,now()) on conflict(notification_id) do update set story=excluded.story,email_sent_at=excluded.email_sent_at,result_published_at=excluded.result_published_at,updated_at=now()", connection);
        command.Parameters.AddWithValue("id", checkpoint.NotificationId); command.Parameters.AddWithValue("user", checkpoint.UserId);
        command.Parameters.AddWithValue("story", (object?)checkpoint.Story ?? DBNull.Value); command.Parameters.AddWithValue("email", (object?)checkpoint.EmailSentAt ?? DBNull.Value); command.Parameters.AddWithValue("result", (object?)checkpoint.ResultPublishedAt ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
