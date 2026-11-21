using ChatApp.Client.UI.Painting;

namespace ChatApp.Client.UI.Controls;

/// <summary>A standalone avatar (used in the user bar).</summary>
public sealed class AvatarControl : Control
{
    private string _displayName = "";
    private bool? _online;

    public AvatarControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(40, 40);
    }

    public string DisplayName { get => _displayName; set { _displayName = value; Invalidate(); } }
    public bool? Online { get => _online; set { _online = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color back = Parent?.BackColor ?? Theme.Sidebar;
        e.Graphics.Clear(back);
        AvatarPainter.Draw(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), _displayName, _online, back);
    }
}
