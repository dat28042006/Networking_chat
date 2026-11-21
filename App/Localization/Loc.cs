using System.Globalization;

namespace ChatApp.Client.Localization;

public enum Language { English, Vietnamese }

/// <summary>
/// Language switch. Forms call Loc.T("key") for every visible text and re-apply their texts
/// when LanguageChanged fires. A missing key is displayed as [key] so it is obvious in the UI.
/// </summary>
public static class Loc
{
    public static Language Current { get; private set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? Language.Vietnamese : Language.English;

    public static event Action? LanguageChanged;

    public static void Set(Language language)
    {
        if (language == Current) return;
        Current = language;
        LanguageChanged?.Invoke();
    }

    public static void Toggle() => Set(Current == Language.English ? Language.Vietnamese : Language.English);

    public static bool Has(string key) => Table.ContainsKey(key);

    public static string T(string key) => Table.TryGetValue(key, out string? value) ? value : $"[{key}]";

    public static string T(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    private static Dictionary<string, string> Table => Current == Language.Vietnamese ? Strings.Vi : Strings.En;
}
