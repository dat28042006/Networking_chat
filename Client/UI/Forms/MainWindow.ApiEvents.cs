using System.Text.Json.Nodes;
using ChatApp.Client.App;
using ChatApp.Client.Localization;
using ChatApp.Client.Models;

namespace ChatApp.Client.UI.Forms;

// DATA COMING IN: everything the network layer tells us. All handlers run on the UI thread.
public sealed partial class MainWindow
{
    private void WireApi()
    {
        _api.ConnectionStateChanged += OnConnectionStateChanged;
        _api.ErrorOccurred += OnErrorOccurred;
        _api.MessageReceived += OnMessageReceived;
        _api.HistoryLoaded += OnHistoryLoaded;
        _api.FriendListChanged += OnFriendListChanged;
        _api.FriendRequestReceived += OnFriendRequestReceived;
        _api.ConversationListChanged += OnConversationListChanged;
        _api.PostReceived += OnPostReceived;
        _api.FeedLoaded += OnFeedLoaded;
        _api.RawEvent += OnRawEvent;
    }

    private void UnwireApi()
    {
        _api.ConnectionStateChanged -= OnConnectionStateChanged;
        _api.ErrorOccurred -= OnErrorOccurred;
        _api.MessageReceived -= OnMessageReceived;
        _api.HistoryLoaded -= OnHistoryLoaded;
        _api.FriendListChanged -= OnFriendListChanged;
        _api.FriendRequestReceived -= OnFriendRequestReceived;
        _api.ConversationListChanged -= OnConversationListChanged;
        _api.PostReceived -= OnPostReceived;
        _api.FeedLoaded -= OnFeedLoaded;
        _api.RawEvent -= OnRawEvent;
    }

    private void OnConnectionStateChanged(bool connected) => UpdateStatusText();

    private void OnErrorOccurred(ApiError error)
    {
        _toast.ShowMessage(ErrorText.For(error), isError: true);
        _friendsView.ShowStatus(ErrorText.For(error), isError: true);
    }

    // ------------------------------------------------------------------ messages

    private void OnMessageReceived(ChatMessage message)
    {
        if (!_cache.Add(message)) return;           // duplicate (history + live event overlap)

        _previews[message.ConvId] = MakePreview(message);
        _conversationList.SetPreview(message.ConvId, _previews[message.ConvId]);

        bool viewingIt = _view == CenterView.Chat && _currentConversation?.Id == message.ConvId;
        if (viewingIt)
        {
            _chat.AddMessage(message);
        }
        else if (message.SenderId != _me.Id)
        {
            _unread[message.ConvId] = _unread.GetValueOrDefault(message.ConvId) + 1;
            _conversationList.SetUnread(message.ConvId, _unread[message.ConvId]);
        }
    }

    private void OnHistoryLoaded(long conversationId, IReadOnlyList<ChatMessage> messages)
    {
        _cache.Merge(messages);
        IReadOnlyList<ChatMessage> all = _cache.Get(conversationId);
        if (all.Count > 0)
        {
            _previews[conversationId] = MakePreview(all[all.Count - 1]);
            _conversationList.SetPreview(conversationId, _previews[conversationId]);
        }
        if (_currentConversation?.Id == conversationId)
            _chat.SetMessages(all);
    }

    private string MakePreview(ChatMessage message)
    {
        string text = message.Body.Replace('\r', ' ').Replace('\n', ' ');
        string who = message.SenderId == _me.Id ? Loc.T("call.you") + ": " : "";
        return who + text;
    }

    // ------------------------------------------------------------------ friends & conversations

    private void OnFriendListChanged(IReadOnlyList<UserInfo> friends)
    {
        _friends = friends;
        _friendsView.SetFriends(friends);
        RefreshConversationList();
        UpdateChatHeader();
    }

    private void OnFriendRequestReceived(FriendRequest request)
    {
        _friendsView.AddRequest(request);
        _toast.ShowMessage(Loc.T("friends.new_request", request.From.Username), isError: false);
    }

    private void OnConversationListChanged(IReadOnlyList<Conversation> conversations)
    {
        _conversations = conversations;
        if (_currentConversation is not null)
            _currentConversation = conversations.FirstOrDefault(c => c.Id == _currentConversation.Id) ?? _currentConversation;

        RefreshConversationList();

        // "Message" on a friend without a chat yet: open it as soon as the server created it.
        if (_pendingDmWithUserId is long userId)
        {
            Conversation? dm = FindDirectConversation(userId);
            if (dm is not null)
            {
                _pendingDmWithUserId = null;
                OpenConversation(dm);
            }
        }
    }

    private void RefreshConversationList()
    {
        var items = _conversations
            .Select(c => (Conversation: c, Title: ConversationNaming.Title(c, _me, _friends)))
            .ToList();

        _conversationList.SetConversations(items, _previews, _unread,
            c => ConversationNaming.OtherOnline(c, _me, _friends));

        if (_currentConversation is not null)
            _conversationList.Select(_currentConversation.Id);
    }

    private void UpdateChatHeader()
    {
        if (_currentConversation is null) return;
        _chat.UpdateHeader(
            ConversationNaming.Title(_currentConversation, _me, _friends),
            ConversationNaming.Subtitle(_currentConversation, _me, _friends));
    }

    // ------------------------------------------------------------------ posts

    private void OnPostReceived(Post post) => _feedView.AddPost(post);
    private void OnFeedLoaded(IReadOnlyList<Post> posts) => _feedView.SetPosts(posts);

    // ------------------------------------------------------------------ media signalling (later phase)

    private void OnRawEvent(string type, JsonObject data)
    {
        // call.* / screen.* events land here. The media layer (Phase 3-5) will subscribe
        // to IApiClient.RawEvent itself; nothing to do in the GUI yet.
    }
}
