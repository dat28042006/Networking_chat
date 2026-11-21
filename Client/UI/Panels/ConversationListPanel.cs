using ChatApp.Client.Localization;
using ChatApp.Client.Models;
using ChatApp.Client.UI.Controls;

namespace ChatApp.Client.UI.Panels;

/// <summary>
/// Left-hand list of conversations with search, "new group" button, previews and unread badges.
/// UI only: it raises events and never talks to the network.
/// </summary>
public sealed class ConversationListPanel : Panel
{
    public event Action<Conversation>? ConversationSelected;
    public event Action? NewGroupRequested;

    private readonly Label _title = new()
    {
        AutoSize = false, Dock = DockStyle.Fill, Font = Theme.Bold, ForeColor = Theme.Muted,
        TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0)
    };
    private readonly Button _newGroup = new() { Text = "＋", Dock = DockStyle.Right, Width = 44, Font = Theme.Header };
    private readonly InputBox _search = new() { Dock = DockStyle.Fill };
    private readonly StackPanel _list = new() { Dock = DockStyle.Fill };
    private readonly ToolTip _tips = new();

    private readonly Dictionary<long, ConversationRow> _rows = new();
    private readonly Dictionary<long, Conversation> _conversations = new();
    private long? _selectedId;

    public ConversationListPanel()
    {
        BackColor = Theme.Sidebar;
        Theme.StyleButton(_newGroup, Theme.Sidebar, Theme.Hover);
        _newGroup.Click += (_, _) => NewGroupRequested?.Invoke();
        _search.Inner.TextChanged += (_, _) => ApplyFilter();

        var header = new Panel { Dock = DockStyle.Top, Height = 40 };
        header.Controls.Add(_title);       // Fill first,
        header.Controls.Add(_newGroup);    // then edge docks

        var searchHost = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(12, 8, 12, 8) };
        searchHost.Controls.Add(_search);

        // Fill control must be added FIRST; later-added edge controls are laid out before it.
        Controls.Add(_list);
        Controls.Add(searchHost);
        Controls.Add(header);

        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        _title.Text = Loc.T("chats.header").ToUpperInvariant();
        _search.PlaceholderText = Loc.T("chats.search");
        _tips.SetToolTip(_newGroup, Loc.T("chats.new_group"));
    }

    /// <summary>Rebuilds the whole list (called whenever the conversation list changes).</summary>
    public void SetConversations(
        IReadOnlyList<(Conversation Conversation, string Title)> items,
        IReadOnlyDictionary<long, string> previews,
        IReadOnlyDictionary<long, int> unread,
        Func<Conversation, bool?> onlineOf)
    {
        _list.SuspendLayout();
        _list.Clear();
        _rows.Clear();
        _conversations.Clear();

        foreach (var (conversation, title) in items)
        {
            var row = new ConversationRow(conversation.Id, title, onlineOf(conversation))
            {
                Preview = previews.TryGetValue(conversation.Id, out string? preview) ? preview : "",
                Unread = unread.TryGetValue(conversation.Id, out int count) ? count : 0,
                IsSelected = conversation.Id == _selectedId
            };
            row.Clicked += OnRowClicked;
            _rows[conversation.Id] = row;
            _conversations[conversation.Id] = conversation;
            _list.Controls.Add(row);
        }

        if (items.Count == 0)
        {
            _list.Controls.Add(new Label
            {
                Text = Loc.T("chats.empty"), AutoSize = false, Height = 60,
                ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter
            });
        }
        _list.ResumeLayout(true);
        ApplyFilter();
    }

    public void SetPreview(long conversationId, string text)
    {
        if (_rows.TryGetValue(conversationId, out ConversationRow? row)) row.Preview = text;
    }

    public void SetUnread(long conversationId, int count)
    {
        if (_rows.TryGetValue(conversationId, out ConversationRow? row)) row.Unread = count;
    }

    /// <summary>Highlights a conversation (does not raise ConversationSelected).</summary>
    public void Select(long conversationId)
    {
        _selectedId = conversationId;
        foreach (var pair in _rows) pair.Value.IsSelected = pair.Key == conversationId;
    }

    private void OnRowClicked(ConversationRow row)
    {
        Select(row.ConversationId);
        if (_conversations.TryGetValue(row.ConversationId, out Conversation? conversation))
            ConversationSelected?.Invoke(conversation);
    }

    private void ApplyFilter()
    {
        string query = _search.Value.Trim();
        foreach (ConversationRow row in _rows.Values)
            row.Visible = query.Length == 0 || row.Title.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
