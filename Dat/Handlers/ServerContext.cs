using System.Text.Json.Nodes;
using ChatApp.Server.Db;

namespace ChatApp.Server;

/// <summary>Everything a handler needs. One instance for the whole server.</summary>
public sealed class ServerContext
{
    public Database Db { get; }
    public SessionManager Sessions { get; }
    public Notifier Notify { get; }

    public ServerContext(Database db, SessionManager sessions, Notifier notify)
    {
        Db = db;
        Sessions = sessions;
        Notify = notify;
    }

    public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>Pushes server events ({"v":1,"type":"evt.*","data":{...}}, no "re") to online users.</summary>
public sealed class Notifier
{
    private readonly TcpServer _server;
    private readonly Database _db;

    public Notifier(TcpServer server, Database db)
    {
        _server = server;
        _db = db;
    }

    public Task SendAsync(IEnumerable<long> userIds, string type, JsonObject data)
        => _server.BroadcastToAsync(userIds, new JsonObject { ["v"] = 1, ["type"] = type, ["data"] = data });

    public Task SendAsync(long userId, string type, JsonObject data)
        => SendAsync(new[] { userId }, type, data);

    /// <summary>Tells every friend of the user that he/she went online or offline.</summary>
    public async Task PresenceChangedAsync(long userId, bool online)
    {
        List<UserBrief> friends = await _db.GetFriendsAsync(userId).ConfigureAwait(false);
        if (friends.Count == 0) return;
        await SendAsync(friends.Select(f => f.Id), MessageTypes.EvtPresence,
            new JsonObject { ["user_id"] = userId, ["online"] = online }).ConfigureAwait(false);
    }
}

/// <summary>JSON shapes of the protocol (field names must match Client/Protocol/ModelParser.cs).</summary>
public static class Wire
{
    public static JsonObject User(long id, string username, bool online) =>
        new() { ["id"] = id, ["username"] = username, ["online"] = online };

    public static JsonObject Message(MessageRow m) => new()
    {
        ["id"] = m.Id,
        ["conv_id"] = m.ConvId,
        ["sender_id"] = m.SenderId,
        ["sender_name"] = m.SenderName,
        ["body"] = m.Body,
        ["ts"] = m.TsMs,
        ["client_msg_id"] = m.ClientMsgId
    };

    public static JsonObject Conversation(ConvRow c)
    {
        var members = new JsonArray();
        foreach (long id in c.Members) members.Add(id);
        return new JsonObject
        {
            ["id"] = c.Id,
            ["name"] = c.Name ?? "",
            ["is_group"] = c.IsGroup,
            ["owner_id"] = c.OwnerId ?? 0,
            ["members"] = members
        };
    }

    public static JsonObject Post(PostRow p) => new()
    {
        ["id"] = p.Id,
        ["author_id"] = p.AuthorId,
        ["author_name"] = p.AuthorName,
        ["caption"] = p.Caption,
        ["ts"] = p.TsMs,
        ["reactions"] = p.Reactions,
        ["comments"] = p.Comments
    };
}
