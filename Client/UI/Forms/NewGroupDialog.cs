using ChatApp.Client.Localization;
using ChatApp.Client.Models;
using ChatApp.Client.UI.Controls;

namespace ChatApp.Client.UI.Forms;

/// <summary>Small dialog: group name + tick the friends to add.</summary>
public sealed class NewGroupDialog : Form
{
    private readonly IReadOnlyList<UserInfo> _friends;
    private readonly InputBox _name = new() { Dock = DockStyle.Top };
    private readonly CheckedListBox _members = new()
    {
        Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Input,
        ForeColor = Theme.Text, Font = Theme.Regular, CheckOnClick = true
    };
    private readonly Label _membersLabel = new() { Dock = DockStyle.Top, Height = 34, ForeColor = Theme.Muted, Font = Theme.SmallBold, TextAlign = ContentAlignment.BottomLeft };
    private readonly Label _error = new() { Dock = DockStyle.Bottom, Height = 28, ForeColor = Color.FromArgb(250, 119, 124), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _create = new() { Width = 110, Height = 38 };
    private readonly Button _cancel = new() { Width = 110, Height = 38, DialogResult = DialogResult.Cancel };

    public NewGroupDialog(IReadOnlyList<UserInfo> friends)
    {
        _friends = friends;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 460);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Regular;
        Padding = new Padding(20);

        Text = Loc.T("group.title");
        _name.PlaceholderText = Loc.T("group.name");
        _name.MaxLength = 40;
        _membersLabel.Text = Loc.T("group.members").ToUpperInvariant();
        _create.Text = Loc.T("group.create");
        _cancel.Text = Loc.T("group.cancel");
        Theme.StylePrimaryButton(_create);
        Theme.StyleNeutralButton(_cancel);

        foreach (UserInfo friend in friends) _members.Items.Add(friend.Username);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0)
        };
        buttons.Controls.Add(_cancel);   // right-to-left: first added = rightmost
        buttons.Controls.Add(_create);

        _create.Click += OnCreateClicked;
        CancelButton = _cancel;

        // Fill first; then Top / Bottom pieces (last added is docked first)
        Controls.Add(_members);
        Controls.Add(_membersLabel);
        Controls.Add(_name);
        Controls.Add(_error);
        Controls.Add(buttons);
    }

    public string GroupName => _name.Value.Trim();

    public IReadOnlyList<long> SelectedUserIds =>
        _members.CheckedIndices.Cast<int>().Select(i => _friends[i].Id).ToList();

    private void OnCreateClicked(object? sender, EventArgs e)
    {
        if (GroupName.Length == 0 || _members.CheckedIndices.Count == 0)
        {
            _error.Text = Loc.T("group.err");
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
