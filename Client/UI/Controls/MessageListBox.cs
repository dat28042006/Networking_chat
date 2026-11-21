using ChatApp.Client.Models;
using ChatApp.Client.UI.Painting;

namespace ChatApp.Client.UI.Controls;

/// <summary>
/// The message history, drawn by hand (owner-draw ListBox): avatar + name + time on the first
/// message of a run, plain wrapped text for the following ones.
/// Known limit: Windows caps a list item at 255 px height, so messages are limited to ~1000 chars
/// (the input box enforces MaxLength = 1000).
/// </summary>
public sealed class MessageListBox : ListBox
{
    private sealed record Row(ChatMessage Message, bool ShowHeader);

    private const int LeftPad = 16;
    private const int AvatarSize = 40;
    private const int Gap = 14;
    private const int RightPad = 20;
    private const int HeaderBlock = 32;     // space taken by "name  time" line (+ top gap)
    private const long GroupWindowMs = 5 * 60 * 1000;

    private const TextFormatFlags BodyFlags =
        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Left |
        TextFormatFlags.Top | TextFormatFlags.NoPadding;

    private ChatMessage? _last;

    public MessageListBox()
    {
        DrawMode = DrawMode.OwnerDrawVariable;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        SelectionMode = SelectionMode.None;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        DoubleBuffered = true;
    }

    // ------------------------------------------------------------------ public API

    public void SetMessages(IEnumerable<ChatMessage> messages)
    {
        BeginUpdate();
        Items.Clear();
        _last = null;
        foreach (ChatMessage message in messages)
            Items.Add(MakeRow(message));
        EndUpdate();
        ScrollToBottom();
    }

    public void AddMessage(ChatMessage message)
    {
        bool wasAtBottom = IsAtBottom();
        Items.Add(MakeRow(message));
        if (wasAtBottom) ScrollToBottom();
    }

    public void ClearMessages()
    {
        Items.Clear();
        _last = null;
    }

    // ------------------------------------------------------------------ helpers

    private Row MakeRow(ChatMessage message)
    {
        bool header = _last is null
                      || _last.SenderId != message.SenderId
                      || message.TsMs - _last.TsMs > GroupWindowMs;
        _last = message;
        return new Row(message, header);
    }

    private bool IsAtBottom()
    {
        if (Items.Count == 0) return true;
        return GetItemRectangle(Items.Count - 1).Bottom <= ClientSize.Height + 4;
    }

    private void ScrollToBottom()
    {
        if (Items.Count > 0) TopIndex = Items.Count - 1;
    }

    // ------------------------------------------------------------------ owner draw

    protected override void OnMeasureItem(MeasureItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var row = (Row)Items[e.Index];

        int textWidth = Math.Max(80, ClientSize.Width - (LeftPad + AvatarSize + Gap) - RightPad);
        Size body = TextRenderer.MeasureText(e.Graphics, row.Message.Body, Theme.Regular,
            new Size(textWidth, int.MaxValue), BodyFlags);

        int height = (row.ShowHeader ? HeaderBlock : 2) + body.Height + 4;
        if (row.ShowHeader) height = Math.Max(height, AvatarSize + 12);
        e.ItemHeight = Math.Min(255, height);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var row = (Row)Items[e.Index];
        Graphics g = e.Graphics;

        using (var back = new SolidBrush(Theme.Background))
            g.FillRectangle(back, e.Bounds);

        int textX = e.Bounds.X + LeftPad + AvatarSize + Gap;
        int y = e.Bounds.Y;

        if (row.ShowHeader)
        {
            AvatarPainter.Draw(g, new Rectangle(e.Bounds.X + LeftPad, y + 8, AvatarSize, AvatarSize),
                row.Message.SenderName, null, Theme.Background);

            const TextFormatFlags line = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            string name = row.Message.SenderName;
            Size nameSize = TextRenderer.MeasureText(g, name, Theme.Bold, new Size(int.MaxValue, int.MaxValue), line);
            TextRenderer.DrawText(g, name, Theme.Bold, new Point(textX, y + 8), Color.White, line);
            TextRenderer.DrawText(g, row.Message.LocalTime.ToString("HH:mm"), Theme.Small,
                new Point(textX + nameSize.Width + 8, y + 11), Theme.Muted, line);
            y += HeaderBlock;
        }
        else
        {
            y += 2;
        }

        var bodyRect = new Rectangle(textX, y, Math.Max(10, e.Bounds.Right - textX - RightPad), Math.Max(10, e.Bounds.Bottom - y));
        TextRenderer.DrawText(g, row.Message.Body, Theme.Regular, bodyRect, Theme.Text, BodyFlags);
    }

    /// <summary>Wrapping depends on the width, so re-measure every item after a resize.</summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Items.Count == 0) return;
        int top = TopIndex;
        BeginUpdate();
        for (int i = 0; i < Items.Count; i++)
        {
            object item = Items[i];
            Items[i] = item;          // re-inserting the same item forces a new MeasureItem
        }
        EndUpdate();
        TopIndex = Math.Min(top, Items.Count - 1);
    }
}
