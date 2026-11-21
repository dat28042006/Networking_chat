namespace ChatApp.Client.UI.Controls;

/// <summary>A thin banner at the bottom that shows an error/info text for a few seconds.</summary>
public sealed class Toast : Label
{
    private readonly System.Windows.Forms.Timer _hideTimer = new() { Interval = 5000 };

    public Toast()
    {
        Dock = DockStyle.Bottom;
        Height = 36;
        TextAlign = ContentAlignment.MiddleCenter;
        Font = Theme.Regular;
        ForeColor = Color.White;
        Visible = false;
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); Visible = false; };
    }

    public void ShowMessage(string text, bool isError)
    {
        Text = text;
        BackColor = isError ? Theme.Danger : Theme.Accent;
        Visible = true;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _hideTimer.Dispose();
        base.Dispose(disposing);
    }
}
