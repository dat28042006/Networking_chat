using ChatApp.Client.Models;
using ChatApp.Client.Net;
using ChatApp.Client.UI.Forms;

namespace ChatApp.Client.App;

/// <summary>
/// Owns the window flow: LoginForm -> MainWindow -> (log out) -> LoginForm ...
/// The application exits when the current window is closed with its X button.
/// </summary>
internal sealed class AppController : ApplicationContext
{
    public AppController()
    {
        ShowLogin();
    }

    private void ShowLogin()
    {
        var login = new LoginForm();
        login.LoginSucceeded += OnLoginSucceeded;
        SwitchTo(login);
    }

    private void OnLoginSucceeded(IApiClient api, LoginResult result)
    {
        var main = new MainWindow(api, result.Me, demoMode: api is MockApiClient);
        main.LoggedOut += ShowLogin;
        SwitchTo(main);
    }

    private void SwitchTo(Form next)
    {
        Form? previous = MainForm;
        MainForm = next;      // re-targets "exit when main form closes" to the new window
        next.Show();
        previous?.Close();
    }
}
