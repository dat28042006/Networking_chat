using ChatApp.Client.Localization;

namespace ChatApp.Client.UI.Forms;

/// <summary>
/// Shows someone's shared screen (one big picture). UI only for now.
/// The media layer calls UpdateFrame(bitmap) ~5 times per second; the form owns and disposes
/// the previous bitmap. In demo mode it draws generated frames.
/// </summary>
public sealed class ScreenViewerForm : Form
{
    private readonly PictureBox _picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
    private readonly System.Windows.Forms.Timer? _demoTimer;
    private int _demoTick;

    public ScreenViewerForm(string sharerName, bool demoFrames)
    {
        Text = Loc.T("screen.title", sharerName);
        ClientSize = new Size(960, 540);
        MinimumSize = new Size(480, 300);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.Black;
        Controls.Add(_picture);

        if (demoFrames)
        {
            _demoTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _demoTimer.Tick += (_, _) => UpdateFrame(MakeDemoFrame());
            _demoTimer.Start();
        }
    }

    /// <summary>Displays a new frame; the previous one is disposed (avoids a memory leak).</summary>
    public void UpdateFrame(Bitmap frame)
    {
        Image? old = _picture.Image;
        _picture.Image = frame;
        old?.Dispose();
    }

    private Bitmap MakeDemoFrame()
    {
        _demoTick++;
        var bitmap = new Bitmap(640, 360);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.FromArgb(30, 40 + _demoTick * 4 % 100, 90));
        g.DrawString($"demo screen frame {_demoTick}", Theme.Title, Brushes.White, new PointF(20, 20));
        return bitmap;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _demoTimer?.Dispose();
            _picture.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
