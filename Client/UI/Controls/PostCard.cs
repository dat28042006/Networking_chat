using ChatApp.Client.Localization;
using ChatApp.Client.Models;
using ChatApp.Client.UI.Painting;

namespace ChatApp.Client.UI.Controls;

/// <summary>One feed post: author, time, caption (wrapped), reaction/comment counts.</summary>
public sealed class PostCard : Control
{
    private const int Pad = 16;
    private const TextFormatFlags CaptionFlags =
        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Left |
        TextFormatFlags.Top | TextFormatFlags.NoPadding;

    private readonly Post _post;

    public PostCard(Post post)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _post = post;
        BackColor = Theme.Sidebar;
        Height = 140;
    }

    public long PostId => _post.Id;

    /// <summary>Height depends on how much the caption wraps, so it follows the width.</summary>
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        int needed = MeasureHeight();
        if (Height != needed) Height = needed;
    }

    private int MeasureHeight()
    {
        int textWidth = Math.Max(100, Width - 2 * Pad);
        Size caption = TextRenderer.MeasureText(_post.Caption, Theme.Regular, new Size(textWidth, int.MaxValue), CaptionFlags);
        return Pad + 40 + 10 + caption.Height + 12 + 20 + Pad;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Sidebar);

        AvatarPainter.Draw(g, new Rectangle(Pad, Pad, 40, 40), _post.AuthorName, null, Theme.Sidebar);

        const TextFormatFlags line = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(g, _post.AuthorName, Theme.Bold, new Point(Pad + 52, Pad + 2), Theme.TextBright, line);
        TextRenderer.DrawText(g, _post.LocalTime.ToString("dd/MM/yyyy HH:mm"), Theme.Small,
            new Point(Pad + 52, Pad + 22), Theme.Muted, line);

        int captionTop = Pad + 40 + 10;
        int textWidth = Math.Max(100, Width - 2 * Pad);
        Size caption = TextRenderer.MeasureText(_post.Caption, Theme.Regular, new Size(textWidth, int.MaxValue), CaptionFlags);
        TextRenderer.DrawText(g, _post.Caption, Theme.Regular,
            new Rectangle(Pad, captionTop, textWidth, caption.Height), Theme.Text, CaptionFlags);

        TextRenderer.DrawText(g, Loc.T("feed.stats", _post.Reactions, _post.Comments), Theme.Small,
            new Point(Pad, captionTop + caption.Height + 12), Theme.Muted, line);
    }
}
