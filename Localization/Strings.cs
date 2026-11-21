namespace ChatApp.Client.Localization;

/// <summary>Holds the two text tables (see Strings.En.cs / Strings.Vi.cs).</summary>
internal static partial class Strings
{
    /// <summary>Keys that exist in English but have no Vietnamese text (debug helper).</summary>
    internal static IEnumerable<string> MissingVietnameseKeys() => En.Keys.Where(key => !Vi.ContainsKey(key));
}
