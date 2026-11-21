using ChatApp.Client.App;
using ChatApp.Client.Localization;
using ChatApp.Client.Models;

namespace ChatApp.Client.UI.Forms;

// USER ACTIONS GOING OUT: clicks and typing in the views are translated into API calls here.
public sealed partial class MainWindow
{
    private void WireViews()
    {
        _navFriends.Click += (_, _) => ShowView(CenterView.Friends);
        _navFeed.Click += (_, _) => { ShowView(CenterView.Feed); _api.LoadFeed(0, 50); };
        _languageButton.Click += (_, _) => Loc.Toggle();
        _logoutButton.Click += (_, _) => LogOut();

        _conversationList.ConversationSelected += OpenConversation;
        _conversationList.NewGroupRequested += OnNewGroupRequested;

        _chat.SendRequested += OnSendRequested;
        _chat.CallRequested += OnCallRequested;
        _chat.ScreenShareRequested += OnScreenShareRequested;

        _friendsView.AddFriendRequested += OnAddFriendRequested;
        _friendsView.RespondRequested += OnRespondRequested;
        _friendsView.MessageRequested += OnMessageFriendRequested;

        _feedView.PostRequested += caption => _api.CreatePost(caption);
    }

    // ------------------------------------------------------------------ conversations & chat

    private void OpenConversation(Conversation conversation)
    {
        _currentConversation = conversation;
        _unread[conversation.Id] = 0;
        _conversationList.SetUnread(conversation.Id, 0);
        _conversationList.Select(conversation.Id);

        ShowView(CenterView.Chat);
        _chat.ShowConversation(
            conversation.Id,
            ConversationNaming.Title(conversation, _me, _friends),
            ConversationNaming.Subtitle(conversation, _me, _friends));

        _chat.SetMessages(_cache.Get(conversation.Id));                 // instant: what we already have
        _api.LoadHistory(conversation.Id, _cache.LastId(conversation.Id), 50);   // then: what we are missing
    }

    private void OnSendRequested(string body)
    {
        if (_currentConversation is null) return;
        _api.SendMessage(_currentConversation.Id, body);                // the echo comes back via MessageReceived
    }

    private Conversation? FindDirectConversation(long userId) =>
        _conversations.FirstOrDefault(c => !c.IsGroup && c.MemberIds.Contains(userId));

    private void OnNewGroupRequested()
    {
        using var dialog = new NewGroupDialog(_friends);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _api.CreateGroup(dialog.GroupName, dialog.SelectedUserIds);
    }

    // ------------------------------------------------------------------ friends

    private void OnAddFriendRequested(string username)
    {
        _api.SendFriendRequest(username);
        _friendsView.ShowStatus(Loc.T("friends.request_sent", username), isError: false);
    }

    private void OnRespondRequested(long requestId, bool accept)
    {
        _api.RespondFriendRequest(requestId, accept);
        _friendsView.RemoveRequest(requestId);
    }

    private void OnMessageFriendRequested(UserInfo friend)
    {
        Conversation? existing = FindDirectConversation(friend.Id);
        if (existing is not null)
        {
            OpenConversation(existing);
            return;
        }
        // No direct chat yet: ask the server to create one, open it when the list updates.
        _pendingDmWithUserId = friend.Id;
        _api.CreateGroup("", new[] { friend.Id });
    }

    // ------------------------------------------------------------------ calls & screen share (UI only for now)

    private IReadOnlyList<UserInfo> ResolveParticipants(Conversation conversation) =>
        conversation.MemberIds
            .Select(id => id == _me.Id ? _me : _friends.FirstOrDefault(f => f.Id == id) ?? new UserInfo(id, $"User {id}"))
            .ToList();

    private void OnCallRequested(bool withVideo)
    {
        if (_currentConversation is null) return;

        if (_callForm is { IsDisposed: false })
        {
            _callForm.Activate();
            return;
        }

        string title = ConversationNaming.Title(_currentConversation, _me, _friends);
        _callForm = new CallForm(title, ResolveParticipants(_currentConversation), _me.Id, withVideo, _demoMode);
        _callForm.FormClosed += (_, _) => _callForm = null;
        _callForm.Show(this);
    }

    private void OnScreenShareRequested()
    {
        if (_screenForm is { IsDisposed: false })
        {
            _screenForm.Activate();
            return;
        }

        _screenForm = new ScreenViewerForm(_me.Username, _demoMode);
        _screenForm.FormClosed += (_, _) => _screenForm = null;
        _screenForm.Show(this);
    }
}
