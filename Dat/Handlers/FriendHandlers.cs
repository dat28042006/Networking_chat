using System.Text.Json.Nodes;
using ChatApp.Server.Db;

namespace ChatApp.Server;

/// <summary>friend.list, friend.request, friend.respond.</summary>
public sealed class FriendHandlers
{
    private readonly ServerContext _ctx;

    public FriendHandlers(ServerContext ctx) => _ctx = ctx;

    public void Register(Dispatcher dispatcher)
    {
        dispatcher.RegisterHandler(MessageTypes.FriendList, ListAsync);
        dispatcher.RegisterHandler(MessageTypes.FriendRequest, RequestAsync);
        dispatcher.RegisterHandler(MessageTypes.FriendRespond, RespondAsync);
    }

    private async Task<JsonObject> ListAsync(ClientConnection conn, JsonObject data)
    {
        List<UserBrief> friends = await _ctx.Db.GetFriendsAsync(conn.UserId).ConfigureAwait(false);
        var array = new JsonArray();
        foreach (UserBrief f in friends)
            array.Add(Wire.User(f.Id, f.Username, _ctx.Sessions.IsOnline(f.Id)));

        // Requests that arrived while the user was offline: pushed once per connection, right after
        // this reply (the main window asks for the friend list as soon as it opens).
        if (!conn.PendingRequestsSent)
        {
            conn.PendingRequestsSent = true;
            long me = conn.UserId;
            conn.AfterReply.Enqueue(async () =>
            {
                foreach (PendingRequestRow p in await _ctx.Db.GetPendingIncomingAsync(me).ConfigureAwait(false))
                    await _ctx.Notify.SendAsync(me, MessageTypes.EvtFriendRequest, new JsonObject
                    {
                        ["request_id"] = p.RequestId,
                        ["from"] = Wire.User(p.FromId, p.FromName, _ctx.Sessions.IsOnline(p.FromId))
                    }).ConfigureAwait(false);
            });
        }

        return Dispatcher.MakeSuccess(0, new JsonObject { ["friends"] = array });
    }

    private async Task<JsonObject> RequestAsync(ClientConnection conn, JsonObject data)
    {
        string username = Req.Str(data, "username", 64).Trim();
        if (username.Length == 0) throw ApiException.BadRequest("Invalid username.");

        UserRow? target = await _ctx.Db.GetUserByNameAsync(username).ConfigureAwait(false);
        if (target is null) throw new ApiException("USER_NOT_FOUND", "No such user.");
        if (target.Id == conn.UserId) throw ApiException.BadRequest("You cannot add yourself.");

        var (result, requestId) = await _ctx.Db.RequestFriendAsync(conn.UserId, target.Id).ConfigureAwait(false);
        long me = conn.UserId;
        string myName = conn.Username;

        switch (result)
        {
            case FriendRequestResult.AlreadyFriends:
                throw new ApiException("ALREADY_FRIENDS", "Already friends.");

            case FriendRequestResult.Created:
                conn.AfterReply.Enqueue(() => _ctx.Notify.SendAsync(target.Id, MessageTypes.EvtFriendRequest,
                    new JsonObject
                    {
                        ["request_id"] = requestId,
                        ["from"] = Wire.User(me, myName, true)
                    }));
                break;

            case FriendRequestResult.AcceptedMutual:
                conn.AfterReply.Enqueue(() => _ctx.Notify.SendAsync(new[] { me, target.Id },
                    MessageTypes.EvtFriendUpdate, new JsonObject()));
                break;
        }
        return Dispatcher.MakeSuccess();
    }

    private async Task<JsonObject> RespondAsync(ClientConnection conn, JsonObject data)
    {
        long requestId = Req.Long(data, "request_id");
        bool accept = Req.Bool(data, "accept");

        long? requester = await _ctx.Db.RespondFriendAsync(conn.UserId, requestId, accept).ConfigureAwait(false);
        if (requester is null) throw ApiException.BadRequest("No such pending request.");

        if (accept)
            conn.AfterReply.Enqueue(() => _ctx.Notify.SendAsync(requester.Value, MessageTypes.EvtFriendUpdate, new JsonObject()));
        return Dispatcher.MakeSuccess();
    }
}
