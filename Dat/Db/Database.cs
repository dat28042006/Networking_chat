using Npgsql;

namespace ChatApp.Server.Db;

public sealed partial class Database : IAsyncDisposable
{
    private readonly NpgsqlDataSource _db;

    public Database(string connectionString) => _db = NpgsqlDataSource.Create(connectionString);

    public static Database FromEnvironment()
    {
        string? cs = Environment.GetEnvironmentVariable("CHAT_DB");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Environment variable CHAT_DB is not set (see docs/POSTGRESQL_SETUP.md).");
        return new Database(cs);
    }

    /// <summary>Health check used at server start.</summary>
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            await using NpgsqlCommand cmd = _db.CreateCommand("SELECT 1");
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] ping failed: {ex.Message}");
            return false;
        }
    }

    /// <returns>The new user id, or null if the username is already taken.</returns>
    public async Task<long?> CreateUserAsync(string username, byte[] passHash, byte[] salt, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "INSERT INTO users (username, pass_hash, salt) VALUES (@u, @h, @s) ON CONFLICT DO NOTHING RETURNING id");
        cmd.Parameters.AddWithValue("u", username);
        cmd.Parameters.AddWithValue("h", passHash);
        cmd.Parameters.AddWithValue("s", salt);
        object? id = await cmd.ExecuteScalarAsync(ct);
        return id is long value ? value : null;
    }

    /// <summary>Stores a message. A repeated clientMsgId returns the ORIGINAL id and timestamp.</summary>
    public async Task<(long Id, long TsMs)> AppendMessageAsync(
        long convId, long senderId, string? clientMsgId, string body, long tsMs, CancellationToken ct = default)
    {
        await using (NpgsqlCommand insert = _db.CreateCommand(
            "INSERT INTO messages (conv_id, sender_id, client_msg_id, body, ts_ms) " +
            "VALUES (@c, @s, @m, @b, @t) ON CONFLICT (sender_id, client_msg_id) DO NOTHING RETURNING id"))
        {
            insert.Parameters.AddWithValue("c", convId);
            insert.Parameters.AddWithValue("s", senderId);
            insert.Parameters.AddWithValue("m", (object?)clientMsgId ?? DBNull.Value);
            insert.Parameters.AddWithValue("b", body);
            insert.Parameters.AddWithValue("t", tsMs);
            if (await insert.ExecuteScalarAsync(ct) is long newId) return (newId, tsMs);
        }

        // Duplicate send: return the stored message so the client gets the same ack.
        await using NpgsqlCommand existing = _db.CreateCommand(
            "SELECT id, ts_ms FROM messages WHERE sender_id = @s AND client_msg_id = @m");
        existing.Parameters.AddWithValue("s", senderId);
        existing.Parameters.AddWithValue("m", clientMsgId ?? "");
        await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct)) return (reader.GetInt64(0), reader.GetInt64(1));
        throw new InvalidOperationException("Message insert failed.");
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
