using ChatApp.Client.App;
using ChatApp.Client.Localization;

namespace ChatApp.Client;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

#if DEBUG
        // Bug-finding aid: tells you in the Output window if a Vietnamese string is missing.
        foreach (string key in Strings.MissingVietnameseKeys())
            System.Diagnostics.Debug.WriteLine($"[Localization] Missing Vietnamese text for key: {key}");
#endif

        // One ApplicationContext owns the whole login -> main window -> logout -> login cycle.
        Application.Run(new AppController());
    }
}
