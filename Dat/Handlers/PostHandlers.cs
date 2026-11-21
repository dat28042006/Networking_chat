using System.Text.Json.Nodes;
using ChatApp.Server.Db;

namespace ChatApp.Server;

/// <summary>post.create, post.feed (text posts only; reactions/comments have no protocol messages yet).</summary>
public sealed class PostHandlers
{
    private const int MaxCaptionChars = 2000;
    private const int MaxFeedLimit = 100;

    private readonly ServerContext _ctx;

    public PostHandlers(ServerContext ctx) => _ctx = ctx;

    public void Register(Dispatcher dispatcher)
    {
        dispatcher.RegisterHandler(MessageTypes.PostCreate, CreateAsync);
        dispatcher.RegisterHandler(MessageTypes.PostFeed, FeedAsync);
    }

    private async Task<JsonObject> CreateAsync(ClientConnection conn, JsonObject data)
    {
        string caption = Req.Str(data, "caption", MaxCaptionChars * 4).Trim();
        if (caption.Length == 0) throw ApiException.BadRequest("Post is empty.");
        if (Req.CharCount(caption) > MaxCaptionChars) throw ApiException.BadRequest($"Post is longer than {MaxCaptionChars} characters.");

        long ts = ServerContext.NowMs();
        long id = await _ctx.Db.CreatePostAsync(conn.UserId, caption, ts).ConfigureAwait(false);

        var post = new PostRow(id, conn.UserId, conn.Username, caption, ts, 0, 0);
        long me = conn.UserId;
        conn.AfterReply.Enqueue(async () =>
        {
            List<UserBrief> friends = await _ctx.Db.GetFriendsAsync(me).ConfigureAwait(false);
            await _ctx.Notify.SendAsync(friends.Select(f => f.Id), MessageTypes.EvtPost, Wire.Post(post)).ConfigureAwait(false);
        });

        return Dispatcher.MakeSuccess(0, new JsonObject { ["post_id"] = id });
    }

    private async Task<JsonObject> FeedAsync(ClientConnection conn, JsonObject data)
    {
        long beforeId = Math.Max(0, Req.OptLong(data, "before_id", 0));
        int limit = (int)Math.Clamp(Req.OptLong(data, "limit", 50), 1, MaxFeedLimit);

        var array = new JsonArray();
        foreach (PostRow p in await _ctx.Db.GetFeedAsync(conn.UserId, beforeId, limit).ConfigureAwait(false))
            array.Add(Wire.Post(p));
        return Dispatcher.MakeSuccess(0, new JsonObject { ["posts"] = array });
    }
}
