using System.Text.Json.Nodes;
using ChatApp.Server.Db;

namespace ChatApp.Server;

/// <summary>msg.send, msg.history, conv.list, conv.create, conv.add_member, conv.remove_member.</summary>
public sealed class ChatHandlers
{
    private const int MaxBodyChars = 1000;      // same limit as the CHECK constraint in the schema
    private const int MaxGroupMembers = 50;
    private const int MaxHistoryLimit = 200;

    private readonly ServerContext _ctx;

    public ChatHandlers(ServerContext ctx) => _ctx = ctx;

    public void Register(Dispatcher dispatcher)
    {
        dispatcher.RegisterHandler(MessageTypes.MsgSend, SendAsync);
        dispatcher.RegisterHandler(MessageTypes.MsgHistory, HistoryAsync);
        dispatcher.RegisterHandler(MessageTypes.ConvList, ListAsync);
        dispatcher.RegisterHandler(MessageTypes.ConvCreate, CreateAsync);
        dispatcher.RegisterHandler(MessageTypes.ConvAddMember, AddMemberAsync);
        dispatcher.RegisterHandler(MessageTypes.ConvRemoveMember, RemoveMemberAsync);
    }

    // ------------------------------------------------------------------ messages

    private async Task<JsonObject> SendAsync(ClientConnection conn, JsonObject data)
    {
        long convId = Req.Long(data, "conv_id");
        string body = Req.Str(data, "body", MaxBodyChars * 4);
        string? clientMsgId = Req.OptStr(data, "client_msg_id", 64);

        if (string.IsNullOrWhiteSpace(body)) throw ApiException.BadRequest("Message is empty.");
        if (Req.CharCount(body) > MaxBodyChars) throw ApiException.BadRequest($"Message is longer than {MaxBodyChars} characters.");

        if (!await _ctx.Db.IsMemberAsync(convId, conn.UserId).ConfigureAwait(false))
            throw new ApiException("NOT_MEMBER", "You are not in this conversation.");

        (long id, long ts) = await _ctx.Db.AppendMessageAsync(
            convId, conn.UserId, clientMsgId, body, ServerContext.NowMs()).ConfigureAwait(false);

        var message = new MessageRow(id, convId, conn.UserId, conn.Username, body, ts, clientMsgId);
        long me = conn.UserId;
        conn.AfterReply.Enqueue(async () =>
        {
            long[] members = (await _ctx.Db.GetConversationAsync(convId).ConfigureAwait(false))?.Members ?? Array.Empty<long>();
            await _ctx.Notify.SendAsync(members.Where(m => m != me), MessageTypes.EvtMsg, Wire.Message(message))
                .ConfigureAwait(false);
        });

        return Dispatcher.MakeSuccess(0, new JsonObject { ["msg_id"] = id, ["ts"] = ts });
    }

    private async Task<JsonObject> HistoryAsync(ClientConnection conn, JsonObject data)
    {
        long convId = Req.Long(data, "conv_id");
        long afterId = Math.Max(0, Req.OptLong(data, "after_id", 0));
        int limit = (int)Math.Clamp(Req.OptLong(data, "limit", 50), 1, MaxHistoryLimit);

        if (!await _ctx.Db.IsMemberAsync(convId, conn.UserId).ConfigureAwait(false))
            throw new ApiException("NOT_MEMBER", "You are not in this conversation.");

        var array = new JsonArray();
        foreach (MessageRow m in await _ctx.Db.GetHistoryAsync(convId, afterId, limit).ConfigureAwait(false))
            array.Add(Wire.Message(m));
        return Dispatcher.MakeSuccess(0, new JsonObject { ["messages"] = array });
    }

    // ------------------------------------------------------------------ conversations

    private async Task<JsonObject> ListAsync(ClientConnection conn, JsonObject data)
    {
        var array = new JsonArray();
        foreach (ConvRow c in await _ctx.Db.ListConversationsAsync(conn.UserId).ConfigureAwait(false))
            array.Add(Wire.Conversation(c));
        return Dispatcher.MakeSuccess(0, new JsonObject { ["conversations"] = array });
    }

    /// <summary>conv.create {name, members[]}: the caller is always added. Two people and no name = direct chat.</summary>
    private async Task<JsonObject> CreateAsync(ClientConnection conn, JsonObject data)
    {
        string name = (Req.OptStr(data, "name", 200) ?? "").Trim();
        if (Req.CharCount(name) > 64) throw ApiException.BadRequest("Group name is too long (max 64).");

        long me = conn.UserId;
        long[] ids = Req.LongArray(data, "members").Append(me).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length < 2) throw ApiException.BadRequest("Pick at least one other member.");
        if (ids.Length > MaxGroupMembers) throw ApiException.BadRequest($"At most {MaxGroupMembers} members.");

        foreach (long other in ids.Where(id => id != me))
            if (!await _ctx.Db.AreFriendsAsync(me, other).ConfigureAwait(false))
                throw new ApiException("NOT_FRIENDS", "You can only chat with your friends.");

        bool isGroup = ids.Length > 2 || name.Length > 0;
        long convId;
        bool created = true;
        if (!isGroup)
        {
            long other = ids.First(id => id != me);
            long? existing = await _ctx.Db.FindDirectConversationAsync(me, other).ConfigureAwait(false);
            if (existing is not null) { convId = existing.Value; created = false; }
            else convId = await _ctx.Db.CreateConversationAsync(false, null, null, ids).ConfigureAwait(false);
        }
        else
        {
            convId = await _ctx.Db.CreateConversationAsync(true, name.Length == 0 ? null : name, me, ids).ConfigureAwait(false);
        }

        if (created)
            conn.AfterReply.Enqueue(() => _ctx.Notify.SendAsync(ids.Where(id => id != me), MessageTypes.EvtConvUpdated,
                new JsonObject { ["conv_id"] = convId }));
        return Dispatcher.MakeSuccess(0, new JsonObject { ["conv_id"] = convId });
    }

    private async Task<ConvRow> RequireOwnedGroupAsync(ClientConnection conn, long convId, bool ownerOnly)
    {
        ConvRow? conv = await _ctx.Db.GetConversationAsync(convId).ConfigureAwait(false);
        if (conv is null || !conv.Members.Contains(conn.UserId))
            throw new ApiException("NOT_MEMBER", "You are not in this conversation.");
        if (!conv.IsGroup) throw ApiException.BadRequest("This is a direct chat, not a group.");
        if (ownerOnly && conv.OwnerId != conn.UserId)
            throw new ApiException("NOT_OWNER", "Only the group owner can do this.");
        return conv;
    }

    private async Task<JsonObject> AddMemberAsync(ClientConnection conn, JsonObject data)
    {
        long convId = Req.Long(data, "conv_id");
        long userId = Req.Long(data, "user_id");

        ConvRow conv = await RequireOwnedGroupAsync(conn, convId, ownerOnly: true).ConfigureAwait(false);
        if (conv.Members.Length >= MaxGroupMembers) throw ApiException.BadRequest($"At most {MaxGroupMembers} members.");
        if (await _ctx.Db.GetUserAsync(userId).ConfigureAwait(false) is null) throw new ApiException("USER_NOT_FOUND", "No such user.");
        if (!await _ctx.Db.AreFriendsAsync(conn.UserId, userId).ConfigureAwait(false))
            throw new ApiException("NOT_FRIENDS", "You can only add your friends.");

        if (await _ctx.Db.AddMemberAsync(convId, userId).ConfigureAwait(false))
        {
            long[] everyone = conv.Members.Append(userId).ToArray();
            conn.AfterReply.Enqueue(() => _ctx.Notify.SendAsync(everyone.Where(id => id != conn.UserId),
                MessageTypes.EvtConvUpdated, new JsonObject { ["conv_id"] = convId }));
        }
        return Dispatcher.MakeSuccess();
    }

    private async Task<JsonObject> RemoveMemberAsync(ClientConnection conn, JsonObject data)
    {
        long convId = Req.Long(data, "conv_id");
        long userId = Req.Long(data, "user_id");

        bool leaving = userId == conn.UserId;
        ConvRow conv = await RequireOwnedGroupAsync(conn, convId, ownerOnly: !leaving).ConfigureAwait(false);
        if (conv.OwnerId == userId) throw ApiException.BadRequest("The owner cannot leave or be removed.");

        if (await _ctx.Db.RemoveMemberAsync(convId, userId).ConfigureAwait(false))
            conn.AfterReply.Enqueue(() => _ctx.Notify.SendAsync(conv.Members.Where(id => id != conn.UserId),
                MessageTypes.EvtConvUpdated, new JsonObject { ["conv_id"] = convId }));
        return Dispatcher.MakeSuccess();
    }
}
