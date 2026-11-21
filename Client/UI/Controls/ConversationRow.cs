using System.Drawing.Drawing2D;
using ChatApp.Client.UI.Painting;

namespace ChatApp.Client.UI.Controls;

/// <summary>One line of the conversation list: avatar, title, last message preview, unread badge.</summary>
public sealed class ConversationRow : Control
{
    private const TextFormatFlags Flags =
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
        TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private bool _hover;
    private bool _selected;
    private string _preview = "";
    private int _unread;

    public ConversationRow(long conversationId, string title, bool? online)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        ConversationId = conversationId;
        Title = title;
        Online = online;
        Height = 60;
        Cursor = Cursors.Hand;
    }

    public long ConversationId { get; }
    public string Title { get; }
    public bool? Online { get; }

    public string Preview { get => _preview; set { _preview = value; Invalidate(); } }
    public int Unread { get => _unread; set { _unread = value; Invalidate(); } }
    public bool IsSelected { get => _selected; set { _selected = value; Invalidate(); } }

    public event Action<ConversationRow>? Clicked;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { base.OnClick(e); Clicked?.Invoke(this); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Color back = _selected ? Theme.Selected : _hover ? Theme.Hover : Theme.Sidebar;
        g.Clear(back);

        AvatarPainter.Draw(g, new Rectangle(12, (Height - 40) / 2, 40, 40), Title, Online, back);

        int textX = 64;
        int rightPad = _unread > 0 ? 46 : 12;
        int textWidth = Math.Max(10, Width - textX - rightPad);
        TextRenderer.DrawText(g, Title, _unread > 0 ? Theme.Bold : Theme.Regular,
            new Rectangle(textX, 10, textWidth, 20), _unread > 0 ? Theme.TextBright : Theme.Text, Flags);
        TextRenderer.DrawText(g, _preview, Theme.Small,
            new Rectangle(textX, 31, textWidth, 18), Theme.Muted, Flags);

        if (_unread > 0)
        {
            var badge = new Rectangle(Width - 38, (Height - 20) / 2, 26, 20);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fill = new SolidBrush(Theme.Danger))
                g.FillEllipse(fill, badge);
            TextRenderer.DrawText(g, _unread > 99 ? "99+" : _unread.ToString(), Theme.SmallBold, badge, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
