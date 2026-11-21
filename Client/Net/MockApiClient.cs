using System.Text.Json.Nodes;
using ChatApp.Client.Models;

namespace ChatApp.Client.Net;

/// <summary>
/// Fake backend so the whole GUI can be built and tested with no server and no database.
/// Behaves like the real thing: replies arrive asynchronously (after a short delay), errors can happen.
///   - Password "wrong"      -> INVALID_CREDENTIALS
///   - Register "admin"      -> USERNAME_TAKEN
///   - Add friend "ghost"    -> USER_NOT_FOUND
///   - Every sent message gets an automatic reply from another member after ~1-2 s
///   - A friend request from "tuan.nguyen" arrives ~5 s after the friend list is first loaded
/// </summary>
public sealed class MockApiClient : ApiClientBase
{
    private readonly Random _random = new();
    private readonly List<UserInfo> _friends = new();
    private readonly List<Conversation> _conversations = new();
    private readonly List<ChatMessage> _messages = new();
    private readonly List<Post> _posts = new();
    private readonly string[] _replies =
    {
        "👍", "Got it!", "Nghe ổn đó!", "Mình sẽ xem lại nhé.", "Haha, đúng rồi 😄", "OK, mai nói tiếp nhé."
    };

    private long _nextMessageId = 1000;
    private long _nextConversationId = 10;
    private long _nextPostId = 500;
    private bool _incomingRequestScheduled;

    // ------------------------------------------------------------------ helpers

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private async void Later(int milliseconds, Action action)
    {
        try
        {
            await Task.Delay(milliseconds);
            if (!IsDisposed) action();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MockApiClient] {ex}");
        }
    }

    private UserInfo? FindUser(long id) => id == Me?.Id ? Me : _friends.FirstOrDefault(f => f.Id == id);

    private void SeedData(UserInfo me)
    {
        _friends.Clear(); _conversations.Clear(); _messages.Clear(); _posts.Clear();
        _friends.AddRange(new[]
        {
            new UserInfo(2, "alice", true),
            new UserInfo(3, "bob", true),
            new UserInfo(4, "minh", false),
            new UserInfo(5, "linh", true),
        });

        _conversations.Add(new Conversation(1, "alice", false, me.Id, new long[] { me.Id, 2 }));
        _conversations.Add(new Conversation(2, "bob", false, me.Id, new long[] { me.Id, 3 }));
        _conversations.Add(new Conversation(3, "Network Project", true, me.Id, new long[] { me.Id, 2, 3 }));
        _conversations.Add(new Conversation(4, "linh", false, me.Id, new long[] { me.Id, 5 }));

        long t = Now();
        _messages.Add(new ChatMessage(1, 1, 2, "alice", "Chào bạn! Mai họp nhóm lúc 9h nhé 😄", t - 3_600_000));
        _messages.Add(new ChatMessage(2, 1, me.Id, me.Username, "Ok, mình sẽ chuẩn bị phần GUI.", t - 3_500_000));
        _messages.Add(new ChatMessage(3, 1, 2, "alice", "Great. The server already answers sys.ping, so you can test the connection first.", t - 3_400_000));
        _messages.Add(new ChatMessage(4, 1, 2, "alice", "Also remember: every message is limited to 1000 characters, and this is a deliberately long line to check that word wrapping works correctly when the window is resized to a narrow width.", t - 3_399_000));
        _messages.Add(new ChatMessage(5, 2, 3, "bob", "Did you push the login screen?", t - 7_200_000));
        _messages.Add(new ChatMessage(6, 3, 3, "bob", "Who is taking the database part?", t - 1_800_000));
        _messages.Add(new ChatMessage(7, 3, 2, "alice", "I did the PostgreSQL tables already.", t - 1_700_000));

        _posts.Add(new Post(1, 2, "alice", "Finished the TCP framing today. 4-byte length prefix works! 🎉", t - 5_400_000, 3, 1));
        _posts.Add(new Post(2, 3, "bob", "Hôm nay thử gọi video qua UDP, hình hơi giật nhưng chạy được rồi.", t - 86_400_000, 5, 2));
        _posts.Add(new Post(3, 5, "linh", "Anyone up for a mini-game session after the demo?", t - 172_800_000, 1, 0));
    }

    // ------------------------------------------------------------------ connection & auth

    public override Task<bool> ConnectAsync(string host, int port)
    {
        RaiseConnectionState(true);
        return Task.FromResult(true);
    }

    public override void Register(string username, string password) => Later(400, () =>
    {
        if (username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            RaiseError(new ApiError("USERNAME_TAKEN", "Username already exists.", 0));
        else
            RaiseRegistered();
    });

    public override void Login(string username, string password) => Later(500, () =>
    {
        if (password == "wrong")
        {
            RaiseError(new ApiError("INVALID_CREDENTIALS", "Wrong username or password.", 0));
            return;
        }
        var me = new UserInfo(1, username, true);
        Me = me;
        SeedData(me);
        RaiseLoggedIn(new LoginResult(me, "0000000000000000", 0, "demo-resume-token"));
    });

    public override void Logout()
    {
        Me = null;
        RaiseConnectionState(false);
    }

    // ------------------------------------------------------------------ lists

    public override void LoadFriends() => Later(200, () =>
    {
        RaiseFriends(_friends.ToList());
        if (_incomingRequestScheduled) return;
        _incomingRequestScheduled = true;
        Later(5000, () => RaiseFriendRequest(new FriendRequest(100, new UserInfo(9, "tuan.nguyen", true))));
    });

    public override void LoadConversations() => Later(200, () => RaiseConversations(_conversations.ToList()));

    // ------------------------------------------------------------------ messaging

    public override void SendMessage(long convId, string body) => Later(250, () =>
    {
        UserInfo? me = Me;
        Conversation? conv = _conversations.FirstOrDefault(c => c.Id == convId);
        if (me is null || conv is null)
        {
            RaiseError(new ApiError("NOT_MEMBER", "You are not in this conversation.", 0));
            return;
        }

        var own = new ChatMessage(++_nextMessageId, convId, me.Id, me.Username, body, Now());
        _messages.Add(own);
        RaiseMessage(own);

        // Simulated reply from another member
        long[] others = conv.MemberIds.Where(id => id != me.Id).ToArray();
        if (others.Length == 0) return;
        UserInfo? replier = FindUser(others[_random.Next(others.Length)]);
        if (replier is null) return;
        Later(1000 + _random.Next(1000), () =>
        {
            var reply = new ChatMessage(++_nextMessageId, convId, replier.Id, replier.Username,
                _replies[_random.Next(_replies.Length)], Now());
            _messages.Add(reply);
            RaiseMessage(reply);
        });
    });

    public override void LoadHistory(long convId, long afterId, int limit) => Later(200, () =>
    {
        var page = _messages
            .Where(m => m.ConvId == convId && m.Id > afterId)
            .OrderBy(m => m.Id)
            .TakeLast(Math.Max(1, limit))
            .ToList();
        RaiseHistory(convId, page);
    });

    // ------------------------------------------------------------------ friends

    public override void SendFriendRequest(string username) => Later(300, () =>
    {
        if (string.IsNullOrWhiteSpace(username) || username.Equals(Me?.Username, StringComparison.OrdinalIgnoreCase))
            RaiseError(new ApiError("BAD_REQUEST", "Invalid username.", 0));
        else if (_friends.Any(f => f.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            RaiseError(new ApiError("ALREADY_FRIENDS", "Already friends.", 0));
        else if (username.Equals("ghost", StringComparison.OrdinalIgnoreCase))
            RaiseError(new ApiError("USER_NOT_FOUND", "No such user.", 0));
        // otherwise: request is pending, nothing to report
    });

    public override void RespondFriendRequest(long requestId, bool accept) => Later(250, () =>
    {
        if (!accept || requestId != 100) return;
        _friends.Add(new UserInfo(9, "tuan.nguyen", true));
        RaiseFriends(_friends.ToList());
    });

    // ------------------------------------------------------------------ conversations

    public override void CreateGroup(string name, IReadOnlyList<long> memberIds) => Later(300, () =>
    {
        UserInfo? me = Me;
        if (me is null) return;
        var members = new List<long> { me.Id };
        members.AddRange(memberIds.Where(id => id != me.Id));
        bool isGroup = members.Count > 2 || !string.IsNullOrWhiteSpace(name);
        string title = name;
        if (!isGroup) title = FindUser(members[1])?.Username ?? "";
        _conversations.Add(new Conversation(++_nextConversationId, title, isGroup, me.Id, members));
        RaiseConversations(_conversations.ToList());
    });

    public override void AddMember(long convId, long userId) => Later(250, () =>
    {
        int index = _conversations.FindIndex(c => c.Id == convId);
        if (index < 0 || _conversations[index].MemberIds.Contains(userId)) return;
        var conv = _conversations[index];
        _conversations[index] = conv with { MemberIds = conv.MemberIds.Append(userId).ToList() };
        RaiseConversations(_conversations.ToList());
    });

    public override void RemoveMember(long convId, long userId) => Later(250, () =>
    {
        int index = _conversations.FindIndex(c => c.Id == convId);
        if (index < 0) return;
        var conv = _conversations[index];
        _conversations[index] = conv with { MemberIds = conv.MemberIds.Where(id => id != userId).ToList() };
        RaiseConversations(_conversations.ToList());
    });

    // ------------------------------------------------------------------ posts

    public override void CreatePost(string caption) => Later(300, () =>
    {
        UserInfo? me = Me;
        if (me is null) return;
        var post = new Post(++_nextPostId, me.Id, me.Username, caption, Now(), 0, 0);
        _posts.Insert(0, post);
        RaisePost(post);
    });

    public override void LoadFeed(long beforeId, int limit) => Later(200, () =>
        RaiseFeed(_posts.OrderByDescending(p => p.TsMs).Take(Math.Max(1, limit)).ToList()));

    public override void SendRaw(string type, JsonObject data)
    {
        // Call/screen signalling is not simulated here; the media layer will use this later.
    }
}
