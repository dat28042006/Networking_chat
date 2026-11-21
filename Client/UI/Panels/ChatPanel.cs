using ChatApp.Client.Localization;
using ChatApp.Client.Models;
using ChatApp.Client.UI.Controls;

namespace ChatApp.Client.UI.Panels;

/// <summary>
/// The conversation view: header (title + call buttons), message list, input bar.
/// UI only: it raises events; MainWindow decides what to do with them.
/// </summary>
public sealed class ChatPanel : Panel
{
    public const int MaxMessageLength = 1000;

    public event Action<string>? SendRequested;
    public event Action<bool>? CallRequested;          // true = with video
    public event Action? ScreenShareRequested;

    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 56, BackColor = Theme.Background };
    private readonly Label _title = new()
    {
        AutoSize = true, Font = Theme.Header, ForeColor = Theme.TextBright,
        BackColor = Color.Transparent, Location = new Point(16, 6)
    };
    private readonly Label _subtitle = new()
    {
        AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted,
        BackColor = Color.Transparent, Location = new Point(17, 33)
    };
    private readonly Button _callButton = MakeIconButton("📞");
    private readonly Button _videoButton = MakeIconButton("🎥");
    private readonly Button _screenButton = MakeIconButton("🖥");

    private readonly MessageListBox _messages = new() { Dock = DockStyle.Fill };
    private readonly Label _placeholder = new()
    {
        Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Header
    };

    private readonly Panel _inputBar = new() { Dock = DockStyle.Bottom, Height = 68, Padding = new Padding(16, 12, 16, 16) };
    private readonly InputBox _input = new() { Dock = DockStyle.Fill, MaxLength = MaxMessageLength };
    private readonly Button _sendButton = new() { Dock = DockStyle.Right, Width = 90 };
    private readonly ToolTip _tips = new();

    private string _title0 = "";

    public ChatPanel()
    {
        BackColor = Theme.Background;

        Theme.StylePrimaryButton(_sendButton);
        _sendButton.Click += (_, _) => Submit();
        _input.Inner.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;     // no "ding", no newline
            Submit();
        };

        _callButton.Click += (_, _) => CallRequested?.Invoke(false);
        _videoButton.Click += (_, _) => CallRequested?.Invoke(true);
        _screenButton.Click += (_, _) => ScreenShareRequested?.Invoke();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, Padding = new Padding(0, 8, 12, 0), BackColor = Color.Transparent
        };
        buttons.Controls.AddRange(new Control[] { _callButton, _videoButton, _screenButton });

        _header.Controls.Add(buttons);
        _header.Controls.Add(_title);
        _header.Controls.Add(_subtitle);
        _header.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Darkest);
            e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
        };

        // Input bar: Fill first, then the Send button on the right
        _inputBar.Controls.Add(_input);
        _inputBar.Controls.Add(_sendButton);

        // Fill controls first, then Bottom, then Top
        Controls.Add(_messages);
        Controls.Add(_placeholder);
        Controls.Add(_inputBar);
        Controls.Add(_header);

        ShowEmptyState();
        ApplyLanguage();
    }

    public long? CurrentConversationId { get; private set; }

    // ------------------------------------------------------------------ public API

    public void ApplyLanguage()
    {
        _placeholder.Text = Loc.T("chat.select");
        _sendButton.Text = Loc.T("chat.send");
        _tips.SetToolTip(_callButton, Loc.T("chat.call"));
        _tips.SetToolTip(_videoButton, Loc.T("chat.video"));
        _tips.SetToolTip(_screenButton, Loc.T("chat.screen"));
        _input.PlaceholderText = Loc.T("chat.placeholder", _title0);
    }

    public void ShowConversation(long conversationId, string title, string subtitle)
    {
        bool changed = CurrentConversationId != conversationId;
        CurrentConversationId = conversationId;
        _title0 = title;
        _title.Text = title;
        _subtitle.Text = subtitle;
        _input.PlaceholderText = Loc.T("chat.placeholder", title);

        _messages.Visible = true;
        _inputBar.Visible = true;
        _header.Visible = true;
        _placeholder.Visible = false;

        if (changed)
        {
            _messages.ClearMessages();
            _input.Value = "";
        }
        _input.Inner.Focus();
    }

    public void UpdateHeader(string title, string subtitle)
    {
        _title0 = title;
        _title.Text = title;
        _subtitle.Text = subtitle;
        _input.PlaceholderText = Loc.T("chat.placeholder", title);
    }

    public void SetMessages(IEnumerable<ChatMessage> messages) => _messages.SetMessages(messages);
    public void AddMessage(ChatMessage message) => _messages.AddMessage(message);

    public void ShowEmptyState()
    {
        CurrentConversationId = null;
        _messages.Visible = false;
        _inputBar.Visible = false;
        _header.Visible = false;
        _placeholder.Visible = true;
    }

    // ------------------------------------------------------------------ internals

    private void Submit()
    {
        string text = _input.Value.Trim();
        if (text.Length == 0) return;
        _input.Value = "";
        SendRequested?.Invoke(text);
    }

    private static Button MakeIconButton(string glyph)
    {
        var button = new Button { Text = glyph, Size = new Size(46, 40), Margin = new Padding(2, 0, 2, 0) };
        Theme.StyleButton(button, Theme.Background, Theme.Hover);
        button.Font = Theme.Emoji;
        return button;
    }
}
