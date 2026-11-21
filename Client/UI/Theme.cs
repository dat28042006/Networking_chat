using System.Globalization;

namespace ChatApp.Client.UI;

/// <summary>Colours and fonts in one place (dark, Discord-like). Restyling the app = edit this file.</summary>
public static class Theme
{
    public static readonly Color Background = Color.FromArgb(49, 51, 56);   // chat area
    public static readonly Color Sidebar = Color.FromArgb(43, 45, 49);      // lists
    public static readonly Color Darkest = Color.FromArgb(30, 31, 34);      // login backdrop, user bar
    public static readonly Color Input = Color.FromArgb(56, 58, 64);
    public static readonly Color Hover = Color.FromArgb(53, 55, 60);
    public static readonly Color Selected = Color.FromArgb(64, 66, 73);
    public static readonly Color Text = Color.FromArgb(219, 222, 225);
    public static readonly Color TextBright = Color.White;
    public static readonly Color Muted = Color.FromArgb(148, 155, 164);
    public static readonly Color Accent = Color.FromArgb(88, 101, 242);
    public static readonly Color AccentHover = Color.FromArgb(71, 82, 196);
    public static readonly Color Danger = Color.FromArgb(218, 55, 60);
    public static readonly Color DangerHover = Color.FromArgb(166, 40, 45);
    public static readonly Color Online = Color.FromArgb(35, 165, 89);

    public static readonly Font Regular = new("Segoe UI", 10f);
    public static readonly Font Bold = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font SmallBold = new("Segoe UI", 8.5f, FontStyle.Bold);
    public static readonly Font Header = new("Segoe UI Semibold", 12f);
    public static readonly Font Title = new("Segoe UI", 20f, FontStyle.Bold);
    public static readonly Font Emoji = new("Segoe UI Emoji", 13f);

    private static readonly Color[] AvatarPalette =
    {
        Color.FromArgb(88, 101, 242), Color.FromArgb(35, 165, 89), Color.FromArgb(240, 178, 50),
        Color.FromArgb(235, 69, 158), Color.FromArgb(237, 66, 69), Color.FromArgb(0, 170, 190),
    };

    /// <summary>Same name -> same colour, everywhere.</summary>
    public static Color AvatarColor(string name)
    {
        int hash = 17;
        foreach (char c in name) hash = unchecked(hash * 31 + c);
        return AvatarPalette[(hash & 0x7FFFFFFF) % AvatarPalette.Length];
    }

    public static string Initial(string name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0) return "?";
        return new StringInfo(trimmed).SubstringByTextElements(0, 1).ToUpperInvariant();
    }

    public static void StyleButton(Button button, Color back, Color hover)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.BackColor = back;
        button.ForeColor = Color.White;
        button.Font = Regular;
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }

    public static void StylePrimaryButton(Button button) => StyleButton(button, Accent, AccentHover);
    public static void StyleDangerButton(Button button) => StyleButton(button, Danger, DangerHover);
    public static void StyleNeutralButton(Button button) => StyleButton(button, Input, Selected);
}
