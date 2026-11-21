using System.Text.Json.Nodes;
using ChatApp.Client.Models;

namespace ChatApp.Client.Net;

/// <summary>
/// The ONLY thing the GUI knows about the network. Two implementations exist:
/// MockApiClient (fake data, no server) and ApiClient (real TCP to the C# server).
/// Actions return immediately; results arrive as events on the UI thread.
/// </summary>
public interface IApiClient : IDisposable
{
    bool IsConnected { get; }
    UserInfo? Me { get; }

    // ----- Events (always raised on the UI thread) -----
    event Action<bool>? ConnectionStateChanged;
    event Action? Registered;                                   // GUI extension: auth.register succeeded
    event Action<LoginResult>? LoggedIn;
    event Action<ApiError>? ErrorOccurred;
    event Action<ChatMessage>? MessageReceived;                 // incoming AND your own sent messages
    event Action<long, IReadOnlyList<ChatMessage>>? HistoryLoaded;
    event Action<IReadOnlyList<UserInfo>>? FriendListChanged;
    event Action<FriendRequest>? FriendRequestReceived;
    event Action<IReadOnlyList<Conversation>>? ConversationListChanged;
    event Action<Post>? PostReceived;
    event Action<IReadOnlyList<Post>>? FeedLoaded;
    event Action<string, JsonObject>? RawEvent;                 // call.* / screen.* for the media layer

    // ----- Actions -----
    Task<bool> ConnectAsync(string host, int port);
    void Register(string username, string password);
    void Login(string username, string password);
    void Logout();

    void LoadFriends();
    void LoadConversations();
    void SendMessage(long convId, string body);
    void LoadHistory(long convId, long afterId, int limit);
    void SendFriendRequest(string username);
    void RespondFriendRequest(long requestId, bool accept);
    void CreateGroup(string name, IReadOnlyList<long> memberIds);
    void AddMember(long convId, long userId);
    void RemoveMember(long convId, long userId);
    void CreatePost(string caption);
    void LoadFeed(long beforeId, int limit);
    void SendRaw(string type, JsonObject data);
}
