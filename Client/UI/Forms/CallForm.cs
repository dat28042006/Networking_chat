using ChatApp.Client.Localization;
using ChatApp.Client.Models;
using ChatApp.Client.UI.Controls;

namespace ChatApp.Client.UI.Forms;

/// <summary>
/// Call window (Discord-like grid, up to 4 tiles + control bar). UI only for now.
///
/// Hooks for the media layer (Phase 3-5):
///   - call UpdateFrame(userId, bitmap) for every decoded video frame,
///   - listen to MicMutedChanged / CameraChanged / ScreenShareChanged / HangUpClicked.
/// In demo mode it feeds generated frames into the second tile to prove the display path works.
/// </summary>
public sealed class CallForm : Form
{
    public event Action<bool>? MicMutedChanged;
    public event Action<bool>? CameraChanged;
    public event Action<bool>? ScreenShareChanged;
    public event Action? HangUpClicked;

    private const int MaxTiles = 4;

    private readonly TableLayoutPanel _grid = new() { Dock = DockStyle.Fill, BackColor = Color.Black, Padding = new Padding(8) };
    private readonly Dictionary<long, VideoTile> _tiles = new();
    private readonly Label _note = new() { Dock = DockStyle.Top, Height = 28, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Black };
    private readonly Button _micButton = MakeButton("🎤", 56);
    private readonly Button _cameraButton = MakeButton("📷", 56);
    private readonly Button _shareButton = MakeButton("🖥", 56);
    private readonly Button _leaveButton = MakeButton("📞", 72);
    private readonly ToolTip _tips = new();

    private readonly System.Windows.Forms.Timer? _demoTimer;
    private readonly long _meId;
    private bool _micMuted;
    private bool _cameraOn;
    private bool _sharing;
    private int _demoTick;

    public CallForm(string conversationTitle, IReadOnlyList<UserInfo> participants, long meId, bool withVideo, bool demoFrames)
    {
        _meId = meId;
        _cameraOn = withVideo;

        Text = Loc.T("call.title", conversationTitle);
        ClientSize = new Size(960, 640);
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.Black;
        ForeColor = Theme.Text;
        Font = Theme.Regular;

        BuildTiles(participants.Take(MaxTiles).ToList());
        BuildControlBar();
        _note.Text = Loc.T("call.note");
        UpdateButtons();

        _tips.SetToolTip(_micButton, Loc.T("call.mic"));
        _tips.SetToolTip(_cameraButton, Loc.T("call.camera"));
        _tips.SetToolTip(_shareButton, Loc.T("call.share"));
        _tips.SetToolTip(_leaveButton, Loc.T("call.leave"));

        if (demoFrames)
        {
            _demoTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _demoTimer.Tick += OnDemoTick;
            _demoTimer.Start();
        }
    }

    // ------------------------------------------------------------------ media-layer API

    /// <summary>Shows a new video frame for a participant. The tile takes ownership of the bitmap.</summary>
    public void UpdateFrame(long userId, Bitmap frame)
    {
        if (_tiles.TryGetValue(userId, out VideoTile? tile)) tile.SetFrame(frame);
        else frame.Dispose();
    }

    // ------------------------------------------------------------------ layout

    private static Button MakeButton(string glyph, int width)
    {
        var button = new Button { Text = glyph, Size = new Size(width, 48), Margin = new Padding(6) };
        Theme.StyleNeutralButton(button);
        button.Font = Theme.Emoji;
        return button;
    }

    private void BuildTiles(IReadOnlyList<UserInfo> people)
    {
        int count = Math.Max(1, people.Count);
        int columns = count == 1 ? 1 : 2;
        int rows = (int)Math.Ceiling(count / (double)columns);

        _grid.ColumnCount = columns;
        _grid.RowCount = rows;
        for (int c = 0; c < columns; c++) _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        for (int r = 0; r < rows; r++) _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

        for (int i = 0; i < people.Count; i++)
        {
            UserInfo person = people[i];
            string name = person.Id == _meId ? Loc.T("call.you") : person.Username;
            var tile = new VideoTile(name) { Dock = DockStyle.Fill, Margin = new Padding(6) };
            _tiles[person.Id] = tile;
            _grid.Controls.Add(tile, i % columns, i / columns);
        }
    }

    private void BuildControlBar()
    {
        var bar = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None, WrapContents = false, BackColor = Color.Black };
        bar.Controls.AddRange(new Control[] { _micButton, _cameraButton, _shareButton, _leaveButton });

        var host = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 84, ColumnCount = 1, RowCount = 1, BackColor = Color.Black };
        host.Controls.Add(bar, 0, 0);

        _micButton.Click += (_, _) => { _micMuted = !_micMuted; MarkMuted(); UpdateButtons(); MicMutedChanged?.Invoke(_micMuted); };
        _cameraButton.Click += (_, _) => { _cameraOn = !_cameraOn; UpdateButtons(); CameraChanged?.Invoke(_cameraOn); };
        _shareButton.Click += (_, _) => { _sharing = !_sharing; UpdateButtons(); ScreenShareChanged?.Invoke(_sharing); };
        _leaveButton.Click += (_, _) => { HangUpClicked?.Invoke(); Close(); };

        // Fill first, then Bottom, then Top
        Controls.Add(_grid);
        Controls.Add(host);
        Controls.Add(_note);
    }

    private void MarkMuted()
    {
        if (_tiles.TryGetValue(_meId, out VideoTile? mine))
        {
            mine.Muted = _micMuted;
            mine.Invalidate();
        }
    }

    private void UpdateButtons()
    {
        _micButton.Text = _micMuted ? "🔇" : "🎤";
        Theme.StyleButton(_micButton, _micMuted ? Theme.Danger : Theme.Input, _micMuted ? Theme.DangerHover : Theme.Selected);
        Theme.StyleButton(_cameraButton, _cameraOn ? Theme.Online : Theme.Input, _cameraOn ? Theme.Online : Theme.Selected);
        Theme.StyleButton(_shareButton, _sharing ? Theme.Online : Theme.Input, _sharing ? Theme.Online : Theme.Selected);
        Theme.StyleDangerButton(_leaveButton);
        foreach (Button b in new[] { _micButton, _cameraButton, _shareButton, _leaveButton }) b.Font = Theme.Emoji;
    }

    // ------------------------------------------------------------------ demo frames

    private void OnDemoTick(object? sender, EventArgs e)
    {
        _demoTick++;
        long target = _tiles.Keys.FirstOrDefault(id => id != _meId);
        if (!_tiles.ContainsKey(target)) return;

        var bitmap = new Bitmap(320, 180);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(40 + _demoTick * 5 % 120, 60, 110 + _demoTick * 3 % 100));
            g.DrawString($"demo frame {_demoTick}", Theme.Header, Brushes.White, new PointF(12, 12));
        }
        UpdateFrame(target, bitmap);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _demoTimer?.Dispose();
        base.Dispose(disposing);
    }
}
