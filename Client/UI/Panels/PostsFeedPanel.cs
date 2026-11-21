using ChatApp.Client.Localization;
using ChatApp.Client.Models;
using ChatApp.Client.UI.Controls;

namespace ChatApp.Client.UI.Panels;

/// <summary>Feed page: composer on top, newest posts first. UI only: raises events.</summary>
public sealed class PostsFeedPanel : Panel
{
    public const int MaxCaptionLength = 500;

    public event Action<string>? PostRequested;

    private readonly Label _title = new()
    {
        Dock = DockStyle.Top, Height = 56, Font = Theme.Header, ForeColor = Theme.TextBright,
        TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(20, 0, 0, 0)
    };
    private readonly Panel _composer = new() { Dock = DockStyle.Top, Height = 72, Padding = new Padding(20, 10, 20, 14) };
    private readonly InputBox _input = new() { Dock = DockStyle.Fill, MaxLength = MaxCaptionLength };
    private readonly Button _postButton = new() { Dock = DockStyle.Right, Width = 100 };
    private readonly StackPanel _list = new() { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(20, 6, 20, 6) };

    private IReadOnlyList<Post> _posts = Array.Empty<Post>();

    public PostsFeedPanel()
    {
        BackColor = Theme.Background;

        Theme.StylePrimaryButton(_postButton);
        _postButton.Click += (_, _) => Submit();
        _input.Inner.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            Submit();
        };

        _composer.Controls.Add(_input);
        _composer.Controls.Add(_postButton);

        Controls.Add(_list);
        Controls.Add(_composer);
        Controls.Add(_title);

        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        _title.Text = Loc.T("feed.title");
        _input.PlaceholderText = Loc.T("feed.placeholder");
        _postButton.Text = Loc.T("feed.post");
        Rebuild();
    }

    public void SetPosts(IReadOnlyList<Post> posts)
    {
        _posts = posts.OrderByDescending(p => p.TsMs).ToList();
        Rebuild();
    }

    public void AddPost(Post post)
    {
        if (_posts.Any(p => p.Id == post.Id)) return;
        _posts = new[] { post }.Concat(_posts).ToList();
        Rebuild();
    }

    private void Submit()
    {
        string caption = _input.Value.Trim();
        if (caption.Length == 0) return;
        _input.Value = "";
        PostRequested?.Invoke(caption);
    }

    private void Rebuild()
    {
        _list.SuspendLayout();
        _list.Clear();
        if (_posts.Count == 0)
        {
            _list.Controls.Add(new Label
            {
                Text = Loc.T("feed.empty"), Height = 70, AutoSize = false,
                ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter
            });
        }
        foreach (Post post in _posts)
        {
            var card = new PostCard(post);
            _list.Controls.Add(card);
            card.Margin = new Padding(0, 0, 0, 10);
        }
        _list.ResumeLayout(true);
    }
}
