using ChatApp.Client.App;
using ChatApp.Client.Localization;
using ChatApp.Client.Models;
using ChatApp.Client.Net;
using ChatApp.Client.UI.Controls;
using ChatApp.Client.UI.Panels;

namespace ChatApp.Client.UI.Forms;

/// <summary>
/// The main window, split over four files so a bug is easy to locate:
///   MainWindow.cs            fields, constructor, lifecycle        (this file)
///   MainWindow.Layout.cs     building the controls, language, status text
///   MainWindow.ApiEvents.cs  reactions to IApiClient events        (data coming IN)
///   MainWindow.ViewEvents.cs reactions to clicks / typing          (user actions going OUT)
/// </summary>
public sealed partial class MainWindow : Form
{
    private enum CenterView { Chat, Friends, Feed }

    // Dependencies
    private readonly IApiClient _api;
    private readonly UserInfo _me;
    private readonly bool _demoMode;

    // Views (each one is UI only and knows nothing about the network)
    private readonly ConversationListPanel _conversationList = new();
    private readonly ChatPanel _chat = new();
    private readonly FriendsPanel _friendsView = new();
    private readonly PostsFeedPanel _feedView = new();

    // Chrome
    private readonly Panel _center = new() { Dock = DockStyle.Fill, BackColor = Theme.Background };
    private readonly Toast _toast = new();
    private readonly Button _navFriends = new();
    private readonly Button _navFeed = new();
    private readonly AvatarControl _meAvatar = new();
    private readonly Label _meName = new();
    private readonly Label _meStatus = new();
    private readonly Button _languageButton = new();
    private readonly Button _logoutButton = new();
    private readonly ToolTip _tips = new();

    // State
    private IReadOnlyList<UserInfo> _friends = Array.Empty<UserInfo>();
    private IReadOnlyList<Conversation> _conversations = Array.Empty<Conversation>();
    private readonly MessageCache _cache = new();
    private readonly Dictionary<long, string> _previews = new();
    private readonly Dictionary<long, int> _unread = new();
    private Conversation? _currentConversation;
    private CenterView _view = CenterView.Chat;
    private long? _pendingDmWithUserId;

    private CallForm? _callForm;
    private ScreenViewerForm? _screenForm;

    /// <summary>Raised when the user logs out; AppController then shows the login form again.</summary>
    public event Action? LoggedOut;

    public MainWindow(IApiClient api, UserInfo me, bool demoMode)
    {
        _api = api;
        _me = me;
        _demoMode = demoMode;

        BuildLayout();
        WireViews();
        WireApi();
        ApplyLanguage();
        ShowView(CenterView.Chat);

        Loc.LanguageChanged += ApplyLanguage;
        FormClosed += OnWindowClosed;

        // Ask for the initial data once everything is subscribed.
        _api.LoadFriends();
        _api.LoadConversations();
    }

    private void OnWindowClosed(object? sender, FormClosedEventArgs e)
    {
        Loc.LanguageChanged -= ApplyLanguage;   // static event: always unsubscribe
        UnwireApi();
        _callForm?.Close();
        _screenForm?.Close();
        _api.Dispose();
    }

    private void LogOut()
    {
        _api.Logout();
        LoggedOut?.Invoke();
    }
}
