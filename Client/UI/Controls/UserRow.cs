using ChatApp.Client.Models;
using ChatApp.Client.UI.Painting;

namespace ChatApp.Client.UI.Controls;

/// <summary>
/// One person in the Friends page: avatar, name, status line and 1-2 action buttons.
///   Friend row  : [Message]
///   Request row : [Accept] [Decline]
/// </summary>
public sealed class UserRow : Control
{
    private const TextFormatFlags Flags =
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
        TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private readonly Button _primary;
    private readonly Button? _secondary;
    private readonly bool? _online;

    public UserRow(UserInfo user, string subtitle, bool? online, string primaryText, string? secondaryText = null)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        User = user;
        Subtitle = subtitle;
        _online = online;
        Height = 62;
        BackColor = Theme.Background;

        _primary = MakeButton(primaryText, secondaryText is null);
        _primary.Click += (_, _) => PrimaryClicked?.Invoke();
        Controls.Add(_primary);

        if (secondaryText is not null)
        {
            _secondary = MakeButton(secondaryText, primary: false, danger: true);
            _secondary.Click += (_, _) => SecondaryClicked?.Invoke();
            Controls.Add(_secondary);
        }
    }

    public UserInfo User { get; }
    public string Subtitle { get; }

    public event Action? PrimaryClicked;
    public event Action? SecondaryClicked;

    private static Button MakeButton(string text, bool primary, bool danger = false)
    {
        var button = new Button { Text = text, Size = new Size(104, 34) };
        if (danger) Theme.StyleDangerButton(button);
        else if (primary) Theme.StyleButton(button, Theme.Input, Theme.Selected);
        else Theme.StylePrimaryButton(button);
        return button;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int x = Width - 16;
        int y = (Height - 34) / 2;
        if (_secondary is not null)
        {
            x -= _secondary.Width;
            _secondary.Location = new Point(x, y);
            x -= 8;
        }
        x -= _primary.Width;
        _primary.Location = new Point(x, y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Background);

        AvatarPainter.Draw(g, new Rectangle(16, (Height - 40) / 2, 40, 40), User.Username, _online, Theme.Background);

        int textWidth = Math.Max(10, _primary.Left - 8 - 68);
        TextRenderer.DrawText(g, User.Username, Theme.Bold, new Rectangle(68, 10, textWidth, 20), Theme.TextBright, Flags);
        TextRenderer.DrawText(g, Subtitle, Theme.Small, new Rectangle(68, 31, textWidth, 18), Theme.Muted, Flags);

        using var line = new Pen(Theme.Sidebar);
        g.DrawLine(line, 16, Height - 1, Width - 16, Height - 1);
    }
}
