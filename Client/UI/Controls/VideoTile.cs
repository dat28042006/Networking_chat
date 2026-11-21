using System.Drawing.Drawing2D;
using ChatApp.Client.UI.Painting;

namespace ChatApp.Client.UI.Controls;

/// <summary>
/// One participant in the call grid: shows an avatar until frames arrive, then the video frame.
/// The media layer will call SetFrame(newBitmap) for every decoded frame.
/// The tile OWNS the bitmap and disposes the previous one: pass a new Bitmap each time.
/// </summary>
public sealed class VideoTile : Control
{
    private Image? _frame;

    public VideoTile(string displayName)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DisplayName = displayName;
        BackColor = Theme.Sidebar;
    }

    public string DisplayName { get; }
    public bool Muted { get; set; }

    public void SetFrame(Image? frame)
    {
        Image? old = _frame;
        _frame = frame;
        old?.Dispose();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Sidebar);

        if (_frame is not null)
        {
            // Letterbox: keep the aspect ratio
            float scale = Math.Min((float)Width / _frame.Width, (float)Height / _frame.Height);
            int w = (int)(_frame.Width * scale), h = (int)(_frame.Height * scale);
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(_frame, (Width - w) / 2, (Height - h) / 2, w, h);
        }
        else
        {
            int size = Math.Min(110, Math.Max(48, Math.Min(Width, Height) / 2));
            AvatarPainter.Draw(g, new Rectangle((Width - size) / 2, (Height - size) / 2 - 10, size, size),
                DisplayName, null, Theme.Sidebar);
        }

        // Name tag, bottom-left
        string label = Muted ? "🔇 " + DisplayName : DisplayName;
        Size textSize = TextRenderer.MeasureText(g, label, Theme.Regular);
        var tag = new Rectangle(10, Height - textSize.Height - 18, textSize.Width + 16, textSize.Height + 8);
        using (var shade = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            g.FillRectangle(shade, tag);
        TextRenderer.DrawText(g, label, Theme.Regular, tag, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _frame?.Dispose(); _frame = null; }
        base.Dispose(disposing);
    }
}
