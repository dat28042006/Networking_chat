using ChatApp.Client.Localization;
using ChatApp.Client.Models;
using ChatApp.Client.Net;
using ChatApp.Client.UI.Controls;

namespace ChatApp.Client.UI.Forms;

/// <summary>
/// Login / register screen. Creates the API client (mock or real), connects, then logs in or
/// registers. On success it hands the connected client to AppController via LoginSucceeded.
/// </summary>
public sealed class LoginForm : Form
{
    public event Action<IApiClient, LoginResult>? LoginSucceeded;

    private readonly Label _title = new() { AutoSize = true, Font = Theme.Title, ForeColor = Theme.TextBright };
    private readonly Label _subtitle = new() { AutoSize = true, ForeColor = Theme.Muted };
    private readonly Label _userLabel = MakeFieldLabel();
    private readonly Label _passLabel = MakeFieldLabel();
    private readonly Label _confirmLabel = MakeFieldLabel();
    private readonly Label _serverLabel = MakeFieldLabel();
    private readonly InputBox _username = new();
    private readonly InputBox _password = new() { IsPassword = true };
    private readonly InputBox _confirm = new() { IsPassword = true };
    private readonly InputBox _server = new() { Value = "127.0.0.1:5000" };
    private readonly CheckBox _demo = new() { AutoSize = true, Checked = true, ForeColor = Theme.Text };
    private readonly Label _message = new() { AutoSize = true, MaximumSize = new Size(376, 0), Font = Theme.Regular };
    private readonly Button _submit = new() { Height = 44 };
    private readonly Button _languageButton = new() { AutoSize = true };
    private readonly LinkLabel _switchMode = new()
    {
        AutoSize = true, LinkColor = Color.FromArgb(0, 168, 252), ActiveLinkColor = Color.White, LinkBehavior = LinkBehavior.HoverUnderline
    };

    private bool _registerMode;
    private IApiClient? _api;

    public LoginForm()
    {
        Text = Loc.T("app.title");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 740);
        MinimumSize = new Size(520, 720);
        BackColor = Theme.Darkest;
        ForeColor = Theme.Text;
        Font = Theme.Regular;

        BuildLayout();
        ApplyLanguage();
        UpdateModeVisibility();

        _submit.Click += OnSubmitClicked;
        _switchMode.LinkClicked += (_, _) => { _registerMode = !_registerMode; ClearMessage(); ApplyLanguage(); UpdateModeVisibility(); };
        _languageButton.Click += (_, _) => Loc.Toggle();
        _demo.CheckedChanged += (_, _) => _server.Enabled = !_demo.Checked;
        _server.Enabled = !_demo.Checked;
        AcceptButton = _submit;

        Loc.LanguageChanged += OnLanguageChanged;
        FormClosed += OnFormClosed;
    }

    // ------------------------------------------------------------------ layout

    private static Label MakeFieldLabel() => new() { AutoSize = true, Font = Theme.SmallBold, ForeColor = Theme.Muted };

    private void BuildLayout()
    {
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, BackColor = Theme.Darkest };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1,
            Padding = new Padding(32), BackColor = Theme.Background
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void AddRow(Control control, int top = 0, DockStyle dock = DockStyle.Fill)
        {
            control.Margin = new Padding(0, top, 0, 0);
            control.Dock = dock;
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Controls.Add(control);
        }

        _languageButton.Margin = new Padding(0);
        Theme.StyleNeutralButton(_languageButton);
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _languageButton.Anchor = AnchorStyles.Right;
        card.Controls.Add(_languageButton);

        _title.Anchor = AnchorStyles.None;
        _subtitle.Anchor = AnchorStyles.None;
        AddRow(_title, 6, DockStyle.None);
        AddRow(_subtitle, 6, DockStyle.None);
        AddRow(_userLabel, 24);
        AddRow(_username, 6);
        AddRow(_passLabel, 16);
        AddRow(_password, 6);
        AddRow(_confirmLabel, 16);
        AddRow(_confirm, 6);
        AddRow(_serverLabel, 16);
        AddRow(_server, 6);
        AddRow(_demo, 12, DockStyle.None);
        AddRow(_message, 12, DockStyle.None);
        Theme.StylePrimaryButton(_submit);
        AddRow(_submit, 16);
        AddRow(_switchMode, 14, DockStyle.None);

        outer.Controls.Add(card, 1, 1);
        Controls.Add(outer);
    }

    private void UpdateModeVisibility()
    {
        _confirmLabel.Visible = _registerMode;
        _confirm.Visible = _registerMode;
    }

    private void ApplyLanguage()
    {
        Text = Loc.T("app.title");
        _title.Text = Loc.T(_registerMode ? "login.register_title" : "login.welcome");
        _subtitle.Text = Loc.T("login.subtitle");
        _subtitle.Visible = !_registerMode;
        _userLabel.Text = Loc.T("login.username");
        _passLabel.Text = Loc.T("login.password");
        _confirmLabel.Text = Loc.T("login.confirm");
        _serverLabel.Text = Loc.T("login.server");
        _demo.Text = Loc.T("login.demo");
        _submit.Text = Loc.T(_registerMode ? "login.submit_register" : "login.submit_login");
        _switchMode.Text = Loc.T(_registerMode ? "login.switch_to_login" : "login.switch_to_register");
        _languageButton.Text = Loc.T("lang.switch_to");
    }

    private void OnLanguageChanged()
    {
        ClearMessage();
        ApplyLanguage();
    }

    // ------------------------------------------------------------------ messages & busy state

    private void ShowMessage(string text, bool isError)
    {
        _message.Text = text;
        _message.ForeColor = isError ? Color.FromArgb(250, 119, 124) : Theme.Online;
    }

    private void ClearMessage() => _message.Text = "";

    private void SetBusy(bool busy)
    {
        _submit.Enabled = !busy;
        _username.Enabled = !busy;
        _password.Enabled = !busy;
        _confirm.Enabled = !busy;
        _demo.Enabled = !busy;
        _server.Enabled = !busy && !_demo.Checked;
        _switchMode.Enabled = !busy;
        UseWaitCursor = busy;
    }

    // ------------------------------------------------------------------ submit flow

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        ClearMessage();
        string user = _username.Value.Trim();
        string pass = _password.Value;

        if (user.Length == 0 || pass.Length == 0) { ShowMessage(Loc.T("login.err_empty"), true); return; }
        if (_registerMode && pass != _confirm.Value) { ShowMessage(Loc.T("login.err_mismatch"), true); return; }
        if (!TryParseServer(out string host, out int port)) { ShowMessage(Loc.T("login.err_server"), true); return; }

        SetBusy(true);
        ShowMessage(Loc.T("login.connecting"), false);

        DisposeApi();
        IApiClient api = _demo.Checked ? new MockApiClient() : new ApiClient();
        _api = api;
        api.ErrorOccurred += OnApiError;
        api.Registered += OnRegistered;
        api.LoggedIn += OnLoggedIn;

        bool connected = await api.ConnectAsync(host, port);
        if (!connected) return;                 // ErrorOccurred already told the user

        if (_registerMode) api.Register(user, pass);
        else api.Login(user, pass);
    }

    private bool TryParseServer(out string host, out int port)
    {
        host = "demo";
        port = 0;
        if (_demo.Checked) return true;

        string[] parts = _server.Value.Trim().Split(':');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0])) return false;
        if (!int.TryParse(parts[1], out port) || port < 1 || port > 65535) return false;
        host = parts[0].Trim();
        return true;
    }

    private void OnApiError(ApiError error)
    {
        SetBusy(false);
        ShowMessage(ErrorText.For(error), true);
    }

    private void OnRegistered()
    {
        SetBusy(false);
        DisposeApi();
        _registerMode = false;
        ApplyLanguage();
        UpdateModeVisibility();
        ShowMessage(Loc.T("login.registered"), false);
    }

    private void OnLoggedIn(LoginResult result)
    {
        IApiClient? api = _api;
        if (api is null) return;
        UnhookApi(api);
        _api = null;                            // ownership moves to MainWindow
        LoginSucceeded?.Invoke(api, result);
    }

    // ------------------------------------------------------------------ cleanup

    private void UnhookApi(IApiClient api)
    {
        api.ErrorOccurred -= OnApiError;
        api.Registered -= OnRegistered;
        api.LoggedIn -= OnLoggedIn;
    }

    private void DisposeApi()
    {
        if (_api is null) return;
        UnhookApi(_api);
        _api.Dispose();
        _api = null;
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        Loc.LanguageChanged -= OnLanguageChanged;   // static event: always unsubscribe
        DisposeApi();
    }
}
