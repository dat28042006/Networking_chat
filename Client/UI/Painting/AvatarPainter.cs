using System.Drawing.Drawing2D;

namespace ChatApp.Client.UI.Painting;

/// <summary>Draws the round coloured avatar with the first letter, plus an optional online dot.</summary>
public static class AvatarPainter
{
    /// <param name="online">null = no status dot, true = green, false = grey.</param>
    /// <param name="dotBorder">Colour behind the avatar (used to cut a ring around the dot).</param>
    public static void Draw(Graphics g, Rectangle bounds, string name, bool? online, Color dotBorder)
    {
        SmoothingMode previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var fill = new SolidBrush(Theme.AvatarColor(name)))
            g.FillEllipse(fill, bounds);

        using (var font = new Font("Segoe UI", bounds.Height * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            TextRenderer.DrawText(g, Theme.Initial(name), font, bounds, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        if (online is bool isOnline)
        {
            int d = Math.Max(10, bounds.Height / 4);
            var dot = new Rectangle(bounds.Right - d, bounds.Bottom - d, d, d);
            using var ring = new SolidBrush(dotBorder);
            using var dotFill = new SolidBrush(isOnline ? Theme.Online : Theme.Muted);
            g.FillEllipse(ring, Rectangle.Inflate(dot, 2, 2));
            g.FillEllipse(dotFill, dot);
        }

        g.SmoothingMode = previous;
    }
}
