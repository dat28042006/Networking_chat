namespace ChatApp.Client.UI.Controls;

/// <summary>
/// Vertical scrolling list: children are stacked top to bottom and always stretched to the
/// panel width (also when the scrollbar appears). Used for conversations, friends and posts.
/// </summary>
public sealed class StackPanel : FlowLayoutPanel
{
    private bool _fitting;

    public StackPanel()
    {
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoScroll = true;
        BackColor = Theme.Sidebar;
        DoubleBuffered = true;
    }

    /// <summary>Removes and disposes every child.</summary>
    public void Clear()
    {
        SuspendLayout();
        foreach (Control child in Controls.Cast<Control>().ToList())
        {
            Controls.Remove(child);
            child.Dispose();
        }
        ResumeLayout(true);
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        if (e.Control is not null) e.Control.Margin = new Padding(0, 0, 0, 2);
        base.OnControlAdded(e);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (!_fitting)
        {
            _fitting = true;
            try
            {
                int width = ClientSize.Width - Padding.Horizontal;
                foreach (Control child in Controls)
                {
                    int target = Math.Max(60, width - child.Margin.Horizontal);
                    if (child.Width != target) child.Width = target;
                }
            }
            finally
            {
                _fitting = false;
            }
        }
        base.OnLayout(levent);
    }
}
