using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using ChatApp.Client.Models;
using ChatApp.Client.Protocol;

namespace ChatApp.Client.Net;

/// <summary>
/// Real client for the C# TcpServer. Handles framing, request/response matching (id -> re),
/// the 15 s sys.ping heartbeat and server-pushed events.
/// NOT done yet (plan phase 6): automatic reconnect with backoff and auth.resume.
/// </summary>
public sealed class ApiClient : ApiClientBase
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly List<UserInfo> _friends = new();
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private long _nextRequestId;

    // ------------------------------------------------------------------ connection

    public override async Task<bool> ConnectAsync(string host, int port)
    {
        Disconnect();
        var tcp = new TcpClient { NoDelay = true };
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await tcp.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            tcp.Dispose();
            RaiseError(new ApiError("CONNECT_FAILED", ex.Message, 0));
            return false;
        }

        _tcp = tcp;
        NetworkStream stream = tcp.GetStream();
        _stream = stream;
        _cts = new CancellationTokenSource();
        CancellationToken ct = _cts.Token;

        _ = Task.Run(() => ReceiveLoopAsync(stream, ct));
        _ = Task.Run(() => HeartbeatLoopAsync(ct));
        RaiseConnectionState(true);
        return true;
    }

    private void Disconnect()
    {
        _stream = null;                       // makes the old receive loop ignore its own shutdown
        try { _cts?.Cancel(); } catch { }
        _cts = null;
        try { _tcp?.Close(); } catch { }
        _tcp = null;
        foreach (var pending in _pending.Values) pending.TrySetCanceled();
        _pending.Clear();
    }

    private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                JsonObject? frame = await FrameCodec.ReadFrameAsync(stream, ct).ConfigureAwait(false);
                if (frame is null) break;
                HandleFrame(frame);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiClient] receive loop ended: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(stream, _stream))   // connection really died (not a deliberate Disconnect)
            {
                Disconnect();
                RaiseConnectionState(false);
            }
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                var data = new JsonObject { ["client_ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                await RequestAsync(MessageTypes.SysPing, data, reportErrors: false).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    // ------------------------------------------------------------------ incoming frames

    private void HandleFrame(JsonObject frame)
    {
        // Reply to one of our requests
        if (frame["re"] is not null)
        {
            if (_pending.TryRemove(ModelParser.L(frame["re"]), out var tcs))
                tcs.TrySetResult(frame);
            return;
        }

        // Connection-level error (e.g. server closes after a bad frame): {"ok":false,"error":{...}}
        if (frame["type"] is null && frame["ok"] is not null && !ModelParser.B(frame["ok"]))
        {
            JsonObject? err = frame["error"] as JsonObject;
            RaiseError(new ApiError(ModelParser.S(err?["code"]), ModelParser.S(err?["message"]), 0));
            return;
        }

        string type = ModelParser.S(frame["type"]);
        JsonObject data = frame["data"] as JsonObject ?? new JsonObject();

        switch (type)
        {
            case MessageTypes.EvtMsg:
                RaiseMessage(ModelParser.ParseMessage(data));
                break;
            case MessageTypes.EvtFriendRequest:
                RaiseFriendRequest(new FriendRequest(ModelParser.L(data["request_id"]), ModelParser.ParseUser(data["from"])));
                break;
            case MessageTypes.EvtFriendUpdate:
                LoadFriends();
                break;
            case MessageTypes.EvtPresence:
                ApplyPresence(ModelParser.L(data["user_id"]), ModelParser.B(data["online"]));
                break;
            case MessageTypes.EvtConvUpdated:
                LoadConversations();
                break;
            case MessageTypes.EvtPost:
                RaisePost(ModelParser.ParsePost(data));
                break;
            default:
                RaiseRaw(type, data);   // call.* / screen.* etc. -> media layer
                break;
        }
    }

    private void ApplyPresence(long userId, bool online)
    {
        List<UserInfo> snapshot;
        lock (_friends)
        {
            int index = _friends.FindIndex(f => f.Id == userId);
            if (index < 0) return;
            _friends[index] = _friends[index] with { Online = online };
            snapshot = _friends.ToList();
        }
        RaiseFriends(snapshot);
    }

    // ------------------------------------------------------------------ request/response

    /// <summary>Returns the reply's "data" object, or null after reporting an error.</summary>
    private async Task<JsonObject?> RequestAsync(string type, JsonObject? data = null, bool reportErrors = true)
    {
        NetworkStream? stream = _stream;
        if (stream is null)
        {
            if (reportErrors) RaiseError(new ApiError("NOT_CONNECTED", "Not connected to the server.", 0));
            return null;
        }

        long id = Interlocked.Increment(ref _nextRequestId);
        var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await FrameCodec.WriteFrameAsync(stream, Envelope.Request(type, id, data), CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }

            Task finished = await Task.WhenAny(tcs.Task, Task.Delay(RequestTimeout)).ConfigureAwait(false);
            if (finished != tcs.Task)
            {
                if (reportErrors) RaiseError(new ApiError("TIMEOUT", $"No reply to {type}.", id));
                return null;
            }

            JsonObject response = await tcs.Task.ConfigureAwait(false);
            if (!ModelParser.B(response["ok"]))
            {
                JsonObject? err = response["error"] as JsonObject;
                if (reportErrors) RaiseError(new ApiError(ModelParser.S(err?["code"]), ModelParser.S(err?["message"]), id));
                return null;
            }
            return response["data"] as JsonObject ?? new JsonObject();
        }
        catch (OperationCanceledException)
        {
            return null;                       // connection closed while waiting
        }
        catch (Exception ex)
        {
            if (reportErrors) RaiseError(new ApiError("NETWORK", ex.Message, id));
            return null;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    /// <summary>Fire-and-forget wrapper: any exception becomes an ErrorOccurred event, never a crash.</summary>
    private async void Fire(Func<Task> work)
    {
        try { await work().ConfigureAwait(false); }
        catch (Exception ex) { RaiseError(new ApiError("INTERNAL", ex.Message, 0)); }
    }

    // ------------------------------------------------------------------ actions

    public override void Register(string username, string password) => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.AuthRegister,
            new JsonObject { ["username"] = username, ["password"] = password });
        if (data is not null) RaiseRegistered();
    });

    public override void Login(string username, string password) => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.AuthLogin,
            new JsonObject { ["username"] = username, ["password"] = password });
        if (data is null) return;
        LoginResult result = ModelParser.ParseLogin(data);
        Me = result.Me;
        RaiseLoggedIn(result);
    });

    public override void Logout() => Fire(async () =>
    {
        await RequestAsync(MessageTypes.AuthLogout, null, reportErrors: false);
        Disconnect();
        Me = null;
    });

    public override void LoadFriends() => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.FriendList);
        if (data is null) return;
        List<UserInfo> snapshot;
        lock (_friends)
        {
            _friends.Clear();
            _friends.AddRange(ModelParser.ParseList(data["friends"], n => ModelParser.ParseUser(n)));
            snapshot = _friends.ToList();
        }
        RaiseFriends(snapshot);
    });

    public override void LoadConversations() => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.ConvList);
        if (data is null) return;
        RaiseConversations(ModelParser.ParseList(data["conversations"], n => ModelParser.ParseConversation(n)));
    });

    public override void SendMessage(long convId, string body) => Fire(async () =>
    {
        UserInfo? me = Me;
        if (me is null) return;
        string clientMsgId = "c-" + Guid.NewGuid().ToString("N")[..8];
        var data = await RequestAsync(MessageTypes.MsgSend, new JsonObject
        {
            ["conv_id"] = convId,
            ["client_msg_id"] = clientMsgId,
            ["body"] = body
        });
        if (data is null) return;
        // The ack carries msg_id + ts. We build our own message so the GUI shows it.
        RaiseMessage(new ChatMessage(ModelParser.L(data["msg_id"]), convId, me.Id, me.Username, body,
            ModelParser.L(data["ts"]), clientMsgId));
    });

    public override void LoadHistory(long convId, long afterId, int limit) => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.MsgHistory,
            new JsonObject { ["conv_id"] = convId, ["after_id"] = afterId, ["limit"] = limit });
        if (data is null) return;
        RaiseHistory(convId, ModelParser.ParseList(data["messages"], n => ModelParser.ParseMessage(n)));
    });

    public override void SendFriendRequest(string username) => Fire(async () =>
    {
        await RequestAsync(MessageTypes.FriendRequest, new JsonObject { ["username"] = username });
    });

    public override void RespondFriendRequest(long requestId, bool accept) => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.FriendRespond,
            new JsonObject { ["request_id"] = requestId, ["accept"] = accept });
        if (data is not null) LoadFriends();
    });

    public override void CreateGroup(string name, IReadOnlyList<long> memberIds) => Fire(async () =>
    {
        var members = new JsonArray(memberIds.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        var data = await RequestAsync(MessageTypes.ConvCreate, new JsonObject { ["name"] = name, ["members"] = members });
        if (data is not null) LoadConversations();
    });

    public override void AddMember(long convId, long userId) => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.ConvAddMember, new JsonObject { ["conv_id"] = convId, ["user_id"] = userId });
        if (data is not null) LoadConversations();
    });

    public override void RemoveMember(long convId, long userId) => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.ConvRemoveMember, new JsonObject { ["conv_id"] = convId, ["user_id"] = userId });
        if (data is not null) LoadConversations();
    });

    public override void CreatePost(string caption) => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.PostCreate, new JsonObject { ["caption"] = caption });
        if (data is not null) LoadFeed(0, 50);   // simplest: reload the feed
    });

    public override void LoadFeed(long beforeId, int limit) => Fire(async () =>
    {
        var data = await RequestAsync(MessageTypes.PostFeed, new JsonObject { ["before_id"] = beforeId, ["limit"] = limit });
        if (data is null) return;
        RaiseFeed(ModelParser.ParseList(data["posts"], n => ModelParser.ParsePost(n)));
    });

    public override void SendRaw(string type, JsonObject data) => Fire(async () =>
    {
        var reply = await RequestAsync(type, data);
        if (reply is not null) RaiseRaw(type, reply);
    });

    public override void Dispose()
    {
        base.Dispose();
        Disconnect();
    }
}
