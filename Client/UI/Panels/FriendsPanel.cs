using ChatApp.Client.Localization;
using ChatApp.Client.Models;
using ChatApp.Client.UI.Controls;

namespace ChatApp.Client.UI.Panels;

/// <summary>
/// Friends page: add a friend by username, "All" tab (online/offline sections) and
/// "Requests" tab (accept / decline). UI only: raises events.
/// </summary>
public sealed class FriendsPanel : Panel
{
    public event Action<string>? AddFriendRequested;
    public event Action<long, bool>? RespondRequested;      // requestId, accept
    public event Action<UserInfo>? MessageRequested;

    private enum Tab { All, Requests }

    private readonly Label _title = new()
    {
        Dock = DockStyle.Top, Height = 56, Font = Theme.Header, ForeColor = Theme.TextBright,
        TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(20, 0, 0, 0)
    };
    private readonly Panel _addRow = new() { Dock = DockStyle.Top, Height = 64, Padding = new Padding(20, 10, 20, 10) };
    private readonly InputBox _addInput = new() { Dock = DockStyle.Fill };
    private readonly Button _addButton = new() { Dock = DockStyle.Right, Width = 100 };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Top, Height = 24, Padding = new Padding(20, 0, 0, 0),
        TextAlign = ContentAlignment.MiddleLeft, Font = Theme.Small, ForeColor = Theme.Muted
    };
    private readonly Panel _tabs = new() { Dock = DockStyle.Top, Height = 44, Padding = new Padding(20, 6, 0, 4) };
    private readonly Button _tabAll = new() { Width = 100, Height = 32 };
    private readonly Button _tabRequests = new() { Width = 130, Height = 32 };
    private readonly StackPanel _list = new() { Dock = DockStyle.Fill, BackColor = Theme.Background };

    private IReadOnlyList<UserInfo> _friends = Array.Empty<UserInfo>();
    private readonly List<FriendRequest> _requests = new();
    private Tab _tab = Tab.All;

    public FriendsPanel()
    {
        BackColor = Theme.Background;

        Theme.StylePrimaryButton(_addButton);
        _addButton.Click += (_, _) => SubmitAdd();
        _addInput.Inner.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            SubmitAdd();
        };

        _addRow.Controls.Add(_addInput);      // Fill first
        _addRow.Controls.Add(_addButton);

        _tabAll.Location = new Point(20, 6);
        _tabRequests.Location = new Point(126, 6);
        _tabAll.Click += (_, _) => SwitchTab(Tab.All);
        _tabRequests.Click += (_, _) => SwitchTab(Tab.Requests);
        _tabs.Controls.Add(_tabAll);
        _tabs.Controls.Add(_tabRequests);

        // Fill first, then top-docked pieces: the LAST one added ends up at the very top.
        Controls.Add(_list);
        Controls.Add(_tabs);
        Controls.Add(_status);
        Controls.Add(_addRow);
        Controls.Add(_title);

        ApplyLanguage();
    }

    // ------------------------------------------------------------------ public API

    public void ApplyLanguage()
    {
        _title.Text = Loc.T("friends.title");
        _addInput.PlaceholderText = Loc.T("friends.add_placeholder");
        _addButton.Text = Loc.T("friends.add");
        _tabAll.Text = Loc.T("friends.tab_all");
        _status.Text = "";
        Rebuild();
    }

    public void SetFriends(IReadOnlyList<UserInfo> friends)
    {
        _friends = friends;
        Rebuild();
    }

    public void AddRequest(FriendRequest request)
    {
        if (_requests.Any(r => r.RequestId == request.RequestId)) return;
        _requests.Add(request);
        Rebuild();
    }

    public void RemoveRequest(long requestId)
    {
        _requests.RemoveAll(r => r.RequestId == requestId);
        Rebuild();
    }

    public void ShowStatus(string text, bool isError)
    {
        _status.Text = text;
        _status.ForeColor = isError ? Theme.Danger : Theme.Online;
    }

    // ------------------------------------------------------------------ internals

    private void SubmitAdd()
    {
        string username = _addInput.Value.Trim();
        if (username.Length == 0) return;
        _addInput.Value = "";
        AddFriendRequested?.Invoke(username);
    }

    private void SwitchTab(Tab tab)
    {
        _tab = tab;
        Rebuild();
    }

    private void Rebuild()
    {
        _tabRequests.Text = Loc.T("friends.tab_requests", _requests.Count);
        Theme.StyleButton(_tabAll, _tab == Tab.All ? Theme.Selected : Theme.Background, Theme.Hover);
        Theme.StyleButton(_tabRequests, _tab == Tab.Requests ? Theme.Selected : Theme.Background, Theme.Hover);

        _list.SuspendLayout();
        _list.Clear();
        if (_tab == Tab.All) BuildFriendRows(); else BuildRequestRows();
        _list.ResumeLayout(true);
    }

    private void BuildFriendRows()
    {
        if (_friends.Count == 0)
        {
            _list.Controls.Add(MakeNote(Loc.T("friends.empty")));
            return;
        }

        var online = _friends.Where(f => f.Online).OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase).ToList();
        var offline = _friends.Where(f => !f.Online).OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase).ToList();
        AddSection(Loc.T("friends.online"), online);
        AddSection(Loc.T("friends.offline"), offline);
    }

    private void AddSection(string title, List<UserInfo> users)
    {
        if (users.Count == 0) return;
        _list.Controls.Add(new Label
        {
            Text = $"{title.ToUpperInvariant()} — {users.Count}", Height = 34, AutoSize = false,
            Font = Theme.SmallBold, ForeColor = Theme.Muted, TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(16, 0, 0, 4)
        });

        foreach (UserInfo user in users)
        {
            string subtitle = user.Online ? Loc.T("friends.online") : Loc.T("friends.offline");
            var row = new UserRow(user, subtitle, user.Online, Loc.T("friends.message"));
            row.PrimaryClicked += () => MessageRequested?.Invoke(user);
            _list.Controls.Add(row);
        }
    }

    private void BuildRequestRows()
    {
        if (_requests.Count == 0)
        {
            _list.Controls.Add(MakeNote(Loc.T("friends.no_requests")));
            return;
        }

        foreach (FriendRequest request in _requests.ToList())
        {
            var row = new UserRow(request.From, Loc.T("friends.incoming"), null,
                Loc.T("friends.accept"), Loc.T("friends.decline"));
            row.PrimaryClicked += () => RespondRequested?.Invoke(request.RequestId, true);
            row.SecondaryClicked += () => RespondRequested?.Invoke(request.RequestId, false);
            _list.Controls.Add(row);
        }
    }

    private static Label MakeNote(string text) => new()
    {
        Text = text, Height = 70, AutoSize = false, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter
    };
}
