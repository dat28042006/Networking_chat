using ChatApp.Client.Localization;
using ChatApp.Client.UI.Controls;

namespace ChatApp.Client.UI.Forms;

public sealed partial class MainWindow
{
    private static readonly Color UserBarColor = Color.FromArgb(35, 36, 40);

    private void BuildLayout()
    {
        Text = Loc.T("app.title");
        ClientSize = new Size(1180, 720);
        MinimumSize = new Size(940, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        Font = Theme.Regular;

        // ---------- centre: three pages on top of each other, one visible at a time
        _chat.Dock = DockStyle.Fill;
        _friendsView.Dock = DockStyle.Fill;
        _feedView.Dock = DockStyle.Fill;
        _center.Controls.Add(_chat);
        _center.Controls.Add(_friendsView);
        _center.Controls.Add(_feedView);
        _center.Controls.Add(_toast);              // added last = docked first (bottom banner)

        // ---------- left column
        var left = new Panel { Dock = DockStyle.Left, Width = 288, BackColor = Theme.Sidebar };

        var nav = new Panel { Dock = DockStyle.Top, Height = 96, Padding = new Padding(8) };
        foreach (Button button in new[] { _navFriends, _navFeed })
        {
            Theme.StyleButton(button, Theme.Sidebar, Theme.Hover);
            button.Dock = DockStyle.Top;
            button.Height = 38;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(10, 0, 0, 0);
            button.ForeColor = Theme.Text;
        }
        nav.Controls.Add(_navFeed);                // last added = on top, so Friends first:
        nav.Controls.Add(_navFriends);

        var userBar = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = UserBarColor };
        _meAvatar.Location = new Point(12, 12);
        _meAvatar.DisplayName = _me.Username;
        _meAvatar.Online = true;
        _meName.AutoSize = true;
        _meName.Font = Theme.Bold;
        _meName.ForeColor = Theme.TextBright;
        _meName.Location = new Point(60, 13);
        _meName.Text = _me.Username;
        _meStatus.AutoSize = true;
        _meStatus.Font = Theme.Small;
        _meStatus.Location = new Point(60, 35);

        Theme.StyleButton(_languageButton, UserBarColor, Theme.Hover);
        _languageButton.Dock = DockStyle.Right;
        _languageButton.Width = 84;
        Theme.StyleButton(_logoutButton, UserBarColor, Theme.DangerHover);
        _logoutButton.Dock = DockStyle.Right;
        _logoutButton.Width = 44;
        _logoutButton.Text = "⏻";
        _logoutButton.Font = Theme.Emoji;

        userBar.Controls.Add(_meAvatar);
        userBar.Controls.Add(_meName);
        userBar.Controls.Add(_meStatus);
        userBar.Controls.Add(_languageButton);     // docked right first...
        userBar.Controls.Add(_logoutButton);       // ...so logout sits at the far right

        _conversationList.Dock = DockStyle.Fill;
        left.Controls.Add(_conversationList);      // Fill first
        left.Controls.Add(userBar);
        left.Controls.Add(nav);

        // Fill (centre) first, then the left column
        Controls.Add(_center);
        Controls.Add(left);
    }

    /// <summary>Re-applies every visible text. Called once at start and on each language switch.</summary>
    private void ApplyLanguage()
    {
        Text = Loc.T("app.title");
        _navFriends.Text = "👥   " + Loc.T("nav.friends");
        _navFeed.Text = "📰   " + Loc.T("nav.feed");
        _languageButton.Text = Loc.T("lang.switch_to");
        _tips.SetToolTip(_logoutButton, Loc.T("user.logout"));

        _conversationList.ApplyLanguage();
        _chat.ApplyLanguage();
        _friendsView.ApplyLanguage();
        _feedView.ApplyLanguage();

        UpdateStatusText();
        RefreshConversationList();
        UpdateChatHeader();
    }

    private void UpdateStatusText()
    {
        if (_demoMode)
        {
            _meStatus.Text = Loc.T("status.demo");
            _meStatus.ForeColor = Theme.Accent;
        }
        else if (_api.IsConnected)
        {
            _meStatus.Text = Loc.T("status.connected");
            _meStatus.ForeColor = Theme.Online;
        }
        else
        {
            _meStatus.Text = Loc.T("status.disconnected");
            _meStatus.ForeColor = Theme.Danger;
        }
    }

    private void ShowView(CenterView view)
    {
        _view = view;
        _chat.Visible = view == CenterView.Chat;
        _friendsView.Visible = view == CenterView.Friends;
        _feedView.Visible = view == CenterView.Feed;

        _navFriends.BackColor = view == CenterView.Friends ? Theme.Selected : Theme.Sidebar;
        _navFeed.BackColor = view == CenterView.Feed ? Theme.Selected : Theme.Sidebar;
    }
}
