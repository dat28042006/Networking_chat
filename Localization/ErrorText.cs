using ChatApp.Client.Models;

namespace ChatApp.Client.Localization;

/// <summary>Turns a server/network error code into a friendly, translated sentence.</summary>
public static class ErrorText
{
    public static string For(ApiError error)
    {
        string key = "err." + error.Code;
        if (Loc.Has(key)) return Loc.T(key);
        string detail = string.IsNullOrWhiteSpace(error.Message) ? error.Code : error.Message;
        return Loc.T("err.generic", detail);
    }
}
