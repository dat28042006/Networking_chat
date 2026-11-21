using System.Text.Json.Nodes;
using ChatApp.Client.Models;

namespace ChatApp.Client.Net;

/// <summary>
/// Shared plumbing: every event is marshalled back to the UI thread through the
/// SynchronizationContext captured at construction. Background threads never touch controls.
/// Create API clients on the UI thread (the LoginForm does).
/// </summary>
public abstract class ApiClientBase : IApiClient
{
    private readonly SynchronizationContext _ui;
    private volatile bool _disposed;

    protected ApiClientBase()
    {
        _ui = SynchronizationContext.Current
              ?? throw new InvalidOperationException("Create the API client on the UI thread so events can return to it.");
    }

    public bool IsConnected { get; protected set; }
    public UserInfo? Me { get; protected set; }
    protected bool IsDisposed => _disposed;

    public event Action<bool>? ConnectionStateChanged;
    public event Action? Registered;
    public event Action<LoginResult>? LoggedIn;
    public event Action<ApiError>? ErrorOccurred;
    public event Action<ChatMessage>? MessageReceived;
    public event Action<long, IReadOnlyList<ChatMessage>>? HistoryLoaded;
    public event Action<IReadOnlyList<UserInfo>>? FriendListChanged;
    public event Action<FriendRequest>? FriendRequestReceived;
    public event Action<IReadOnlyList<Conversation>>? ConversationListChanged;
    public event Action<Post>? PostReceived;
    public event Action<IReadOnlyList<Post>>? FeedLoaded;
    public event Action<string, JsonObject>? RawEvent;

    /// <summary>Runs the action on the UI thread unless the client has been disposed.</summary>
    protected void OnUi(Action action)
    {
        if (_disposed) return;
        _ui.Post(_ => { if (!_disposed) action(); }, null);
    }

    protected void RaiseConnectionState(bool connected)
    {
        IsConnected = connected;
        OnUi(() => ConnectionStateChanged?.Invoke(connected));
    }
    protected void RaiseRegistered() => OnUi(() => Registered?.Invoke());
    protected void RaiseLoggedIn(LoginResult result) => OnUi(() => LoggedIn?.Invoke(result));
    protected void RaiseError(ApiError error) => OnUi(() => ErrorOccurred?.Invoke(error));
    protected void RaiseMessage(ChatMessage message) => OnUi(() => MessageReceived?.Invoke(message));
    protected void RaiseHistory(long convId, IReadOnlyList<ChatMessage> messages) => OnUi(() => HistoryLoaded?.Invoke(convId, messages));
    protected void RaiseFriends(IReadOnlyList<UserInfo> friends) => OnUi(() => FriendListChanged?.Invoke(friends));
    protected void RaiseFriendRequest(FriendRequest request) => OnUi(() => FriendRequestReceived?.Invoke(request));
    protected void RaiseConversations(IReadOnlyList<Conversation> conversations) => OnUi(() => ConversationListChanged?.Invoke(conversations));
    protected void RaisePost(Post post) => OnUi(() => PostReceived?.Invoke(post));
    protected void RaiseFeed(IReadOnlyList<Post> posts) => OnUi(() => FeedLoaded?.Invoke(posts));
    protected void RaiseRaw(string type, JsonObject data) => OnUi(() => RawEvent?.Invoke(type, data));

    public abstract Task<bool> ConnectAsync(string host, int port);
    public abstract void Register(string username, string password);
    public abstract void Login(string username, string password);
    public abstract void Logout();
    public abstract void LoadFriends();
    public abstract void LoadConversations();
    public abstract void SendMessage(long convId, string body);
    public abstract void LoadHistory(long convId, long afterId, int limit);
    public abstract void SendFriendRequest(string username);
    public abstract void RespondFriendRequest(long requestId, bool accept);
    public abstract void CreateGroup(string name, IReadOnlyList<long> memberIds);
    public abstract void AddMember(long convId, long userId);
    public abstract void RemoveMember(long convId, long userId);
    public abstract void CreatePost(string caption);
    public abstract void LoadFeed(long beforeId, int limit);
    public abstract void SendRaw(string type, JsonObject data);

    public virtual void Dispose() => _disposed = true;
}
