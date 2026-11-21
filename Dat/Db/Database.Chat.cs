using Npgsql;

namespace ChatApp.Server.Db;

// Queries for the chat features. Every statement uses parameters (never string concatenation).
public sealed partial class Database
{
    // ------------------------------------------------------------------ users

    public async Task<UserRow?> GetUserByNameAsync(string username, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "SELECT id, username, pass_hash, salt FROM users WHERE lower(username) = lower(@u)");
        cmd.Parameters.AddWithValue("u", username);
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new UserRow(r.GetInt64(0), r.GetString(1), r.GetFieldValue<byte[]>(2), r.GetFieldValue<byte[]>(3));
    }

    public async Task<UserBrief?> GetUserAsync(long id, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand("SELECT id, username FROM users WHERE id = @id");
        cmd.Parameters.AddWithValue("id", id);
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new UserBrief(r.GetInt64(0), r.GetString(1)) : null;
    }

    // ------------------------------------------------------------------ friends

    public async Task<List<UserBrief>> GetFriendsAsync(long me, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "SELECT u.id, u.username FROM friendships f " +
            "JOIN users u ON u.id = CASE WHEN f.user_a = @me THEN f.user_b ELSE f.user_a END " +
            "WHERE f.status = 'accepted' AND (f.user_a = @me OR f.user_b = @me) ORDER BY u.username");
        cmd.Parameters.AddWithValue("me", me);
        var list = new List<UserBrief>();
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new UserBrief(r.GetInt64(0), r.GetString(1)));
        return list;
    }

    public async Task<bool> AreFriendsAsync(long a, long b, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "SELECT 1 FROM friendships WHERE user_a = @lo AND user_b = @hi AND status = 'accepted'");
        cmd.Parameters.AddWithValue("lo", Math.Min(a, b));
        cmd.Parameters.AddWithValue("hi", Math.Max(a, b));
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<(FriendRequestResult Result, long RequestId)> RequestFriendAsync(long me, long other, CancellationToken ct = default)
    {
        long lo = Math.Min(me, other), hi = Math.Max(me, other);

        await using (NpgsqlCommand insert = _db.CreateCommand(
            "INSERT INTO friendships (user_a, user_b, requested_by) VALUES (@lo, @hi, @me) " +
            "ON CONFLICT (user_a, user_b) DO NOTHING RETURNING id"))
        {
            insert.Parameters.AddWithValue("lo", lo);
            insert.Parameters.AddWithValue("hi", hi);
            insert.Parameters.AddWithValue("me", me);
            if (await insert.ExecuteScalarAsync(ct) is long newId) return (FriendRequestResult.Created, newId);
        }

        long id, requestedBy;
        string status;
        await using (NpgsqlCommand select = _db.CreateCommand(
            "SELECT id, requested_by, status FROM friendships WHERE user_a = @lo AND user_b = @hi"))
        {
            select.Parameters.AddWithValue("lo", lo);
            select.Parameters.AddWithValue("hi", hi);
            await using NpgsqlDataReader r = await select.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Friendship row vanished.");
            id = r.GetInt64(0);
            requestedBy = r.GetInt64(1);
            status = r.GetString(2);
        }

        if (status == "accepted") return (FriendRequestResult.AlreadyFriends, id);
        if (requestedBy == me) return (FriendRequestResult.AlreadyPending, id);

        // The other person had already asked us: asking back means "yes".
        await using NpgsqlCommand accept = _db.CreateCommand(
            "UPDATE friendships SET status = 'accepted' WHERE id = @id AND status = 'pending'");
        accept.Parameters.AddWithValue("id", id);
        await accept.ExecuteNonQueryAsync(ct);
        return (FriendRequestResult.AcceptedMutual, id);
    }

    /// <returns>The id of the user who sent the request, or null if it does not exist / is not addressed to <paramref name="me"/>.</returns>
    public async Task<long?> RespondFriendAsync(long me, long requestId, bool accept, CancellationToken ct = default)
    {
        long requester;
        await using (NpgsqlCommand select = _db.CreateCommand(
            "SELECT requested_by FROM friendships WHERE id = @id AND status = 'pending' " +
            "AND requested_by <> @me AND (user_a = @me OR user_b = @me)"))
        {
            select.Parameters.AddWithValue("id", requestId);
            select.Parameters.AddWithValue("me", me);
            if (await select.ExecuteScalarAsync(ct) is not long found) return null;
            requester = found;
        }

        await using NpgsqlCommand change = _db.CreateCommand(accept
            ? "UPDATE friendships SET status = 'accepted' WHERE id = @id"
            : "DELETE FROM friendships WHERE id = @id");
        change.Parameters.AddWithValue("id", requestId);
        await change.ExecuteNonQueryAsync(ct);
        return requester;
    }

    public async Task<List<PendingRequestRow>> GetPendingIncomingAsync(long me, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "SELECT f.id, u.id, u.username FROM friendships f JOIN users u ON u.id = f.requested_by " +
            "WHERE f.status = 'pending' AND f.requested_by <> @me AND (f.user_a = @me OR f.user_b = @me) ORDER BY f.id");
        cmd.Parameters.AddWithValue("me", me);
        var list = new List<PendingRequestRow>();
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new PendingRequestRow(r.GetInt64(0), r.GetInt64(1), r.GetString(2)));
        return list;
    }

    // ------------------------------------------------------------------ conversations

    private const string ConvSelect =
        "SELECT c.id, c.name, c.type, c.owner_id, " +
        "(SELECT array_agg(m.user_id ORDER BY m.joined_at, m.user_id) FROM conversation_members m WHERE m.conv_id = c.id) " +
        "FROM conversations c ";

    private static ConvRow ReadConv(NpgsqlDataReader r) => new(
        r.GetInt64(0),
        r.IsDBNull(1) ? null : r.GetString(1),
        r.GetString(2) == "group",
        r.IsDBNull(3) ? null : r.GetInt64(3),
        r.IsDBNull(4) ? Array.Empty<long>() : r.GetFieldValue<long[]>(4));

    public async Task<List<ConvRow>> ListConversationsAsync(long userId, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            ConvSelect + "WHERE c.id IN (SELECT conv_id FROM conversation_members WHERE user_id = @u) ORDER BY c.id");
        cmd.Parameters.AddWithValue("u", userId);
        var list = new List<ConvRow>();
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(ReadConv(r));
        return list;
    }

    public async Task<ConvRow?> GetConversationAsync(long convId, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(ConvSelect + "WHERE c.id = @c");
        cmd.Parameters.AddWithValue("c", convId);
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadConv(r) : null;
    }

    public async Task<long?> FindDirectConversationAsync(long a, long b, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "SELECT c.id FROM conversations c WHERE c.type = 'dm' " +
            "AND EXISTS (SELECT 1 FROM conversation_members m WHERE m.conv_id = c.id AND m.user_id = @a) " +
            "AND EXISTS (SELECT 1 FROM conversation_members m WHERE m.conv_id = c.id AND m.user_id = @b) LIMIT 1");
        cmd.Parameters.AddWithValue("a", a);
        cmd.Parameters.AddWithValue("b", b);
        return await cmd.ExecuteScalarAsync(ct) is long id ? id : null;
    }

    /// <summary>Creates a conversation and its members in one transaction. ownerId is null for direct chats.</summary>
    public async Task<long> CreateConversationAsync(bool isGroup, string? name, long? ownerId, long[] memberIds, CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _db.OpenConnectionAsync(ct);
        await using NpgsqlTransaction tx = await conn.BeginTransactionAsync(ct);

        long convId;
        await using (var insert = new NpgsqlCommand(
            "INSERT INTO conversations (type, name, owner_id) VALUES (@t, @n, @o) RETURNING id", conn, tx))
        {
            insert.Parameters.AddWithValue("t", isGroup ? "group" : "dm");
            insert.Parameters.AddWithValue("n", (object?)name ?? DBNull.Value);
            insert.Parameters.AddWithValue("o", (object?)ownerId ?? DBNull.Value);
            convId = Convert.ToInt64(await insert.ExecuteScalarAsync(ct));
        }

        await using (var members = new NpgsqlCommand(
            "INSERT INTO conversation_members (conv_id, user_id, role) " +
            "SELECT @c, u, CASE WHEN u = @o THEN 'owner' ELSE 'member' END FROM unnest(@ids) AS u", conn, tx))
        {
            members.Parameters.AddWithValue("c", convId);
            members.Parameters.AddWithValue("o", (object?)ownerId ?? DBNull.Value);
            members.Parameters.AddWithValue("ids", memberIds);
            await members.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return convId;
    }

    public async Task<bool> IsMemberAsync(long convId, long userId, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "SELECT 1 FROM conversation_members WHERE conv_id = @c AND user_id = @u");
        cmd.Parameters.AddWithValue("c", convId);
        cmd.Parameters.AddWithValue("u", userId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<bool> AddMemberAsync(long convId, long userId, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "INSERT INTO conversation_members (conv_id, user_id) VALUES (@c, @u) ON CONFLICT DO NOTHING");
        cmd.Parameters.AddWithValue("c", convId);
        cmd.Parameters.AddWithValue("u", userId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> RemoveMemberAsync(long convId, long userId, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "DELETE FROM conversation_members WHERE conv_id = @c AND user_id = @u");
        cmd.Parameters.AddWithValue("c", convId);
        cmd.Parameters.AddWithValue("u", userId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ------------------------------------------------------------------ messages

    /// <summary>
    /// afterId &gt; 0: the next <paramref name="limit"/> messages after that id (oldest first).
    /// afterId = 0: the LATEST <paramref name="limit"/> messages, returned oldest first.
    /// </summary>
    public async Task<List<MessageRow>> GetHistoryAsync(long convId, long afterId, int limit, CancellationToken ct = default)
    {
        const string columns = "m.id, m.conv_id, m.sender_id, u.username, m.body, m.ts_ms, m.client_msg_id";
        string sql = afterId > 0
            ? $"SELECT {columns} FROM messages m JOIN users u ON u.id = m.sender_id " +
              "WHERE m.conv_id = @c AND m.id > @a ORDER BY m.id LIMIT @l"
            : $"SELECT * FROM (SELECT {columns} FROM messages m JOIN users u ON u.id = m.sender_id " +
              "WHERE m.conv_id = @c ORDER BY m.id DESC LIMIT @l) latest ORDER BY id";

        await using NpgsqlCommand cmd = _db.CreateCommand(sql);
        cmd.Parameters.AddWithValue("c", convId);
        if (afterId > 0) cmd.Parameters.AddWithValue("a", afterId);
        cmd.Parameters.AddWithValue("l", limit);

        var list = new List<MessageRow>();
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new MessageRow(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetString(3), r.GetString(4),
                r.GetInt64(5), r.IsDBNull(6) ? null : r.GetString(6)));
        return list;
    }

    // ------------------------------------------------------------------ posts

    public async Task<long> CreatePostAsync(long authorId, string caption, long tsMs, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "INSERT INTO posts (author_id, caption, ts_ms) VALUES (@a, @c, @t) RETURNING id");
        cmd.Parameters.AddWithValue("a", authorId);
        cmd.Parameters.AddWithValue("c", caption);
        cmd.Parameters.AddWithValue("t", tsMs);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    /// <summary>Posts of me and my accepted friends, newest first. beforeId = 0 means "from the newest".</summary>
    public async Task<List<PostRow>> GetFeedAsync(long me, long beforeId, int limit, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "SELECT p.id, p.author_id, u.username, p.caption, p.ts_ms, " +
            "(SELECT count(*) FROM post_reactions r WHERE r.post_id = p.id), " +
            "(SELECT count(*) FROM post_comments c WHERE c.post_id = p.id) " +
            "FROM posts p JOIN users u ON u.id = p.author_id " +
            "WHERE (@before = 0 OR p.id < @before) AND (p.author_id = @me OR EXISTS (" +
            "  SELECT 1 FROM friendships f WHERE f.status = 'accepted' " +
            "  AND f.user_a = LEAST(@me, p.author_id) AND f.user_b = GREATEST(@me, p.author_id))) " +
            "ORDER BY p.id DESC LIMIT @l");
        cmd.Parameters.AddWithValue("me", me);
        cmd.Parameters.AddWithValue("before", beforeId);
        cmd.Parameters.AddWithValue("l", limit);

        var list = new List<PostRow>();
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new PostRow(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetInt64(4),
                r.GetInt64(5), r.GetInt64(6)));
        return list;
    }
}
