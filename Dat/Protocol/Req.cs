using System.Text.Json.Nodes;

namespace ChatApp.Server;

/// <summary>Reads request fields; a missing or wrong-typed field becomes BAD_REQUEST (never a crash).</summary>
public static class Req
{
    public static string Str(JsonObject d, string key, int maxLen = 1000)
    {
        if (d[key] is JsonValue v && v.TryGetValue<string>(out string? s) && s is not null)
        {
            if (s.Length > maxLen) throw ApiException.BadRequest($"'{key}' is too long.");
            return s;
        }
        throw ApiException.BadRequest($"Missing or invalid '{key}'.");
    }

    public static string? OptStr(JsonObject d, string key, int maxLen = 1000)
        => d[key] is null ? null : Str(d, key, maxLen);

    public static long Long(JsonObject d, string key)
    {
        if (d[key] is JsonValue v && v.TryGetValue<long>(out long n)) return n;
        throw ApiException.BadRequest($"Missing or invalid '{key}'.");
    }

    public static long OptLong(JsonObject d, string key, long fallback)
        => d[key] is null ? fallback : Long(d, key);

    public static bool Bool(JsonObject d, string key)
    {
        if (d[key] is JsonValue v && v.TryGetValue<bool>(out bool b)) return b;
        throw ApiException.BadRequest($"Missing or invalid '{key}'.");
    }

    public static long[] LongArray(JsonObject d, string key, int maxCount = 100)
    {
        if (d[key] is not JsonArray arr) throw ApiException.BadRequest($"Missing or invalid '{key}'.");
        if (arr.Count > maxCount) throw ApiException.BadRequest($"'{key}' has too many items.");
        var list = new List<long>();
        foreach (JsonNode? item in arr)
        {
            if (item is JsonValue v && v.TryGetValue<long>(out long n)) list.Add(n);
            else throw ApiException.BadRequest($"Invalid item in '{key}'.");
        }
        return list.ToArray();
    }

    /// <summary>Number of Unicode code points, the same unit as PostgreSQL's char_length().</summary>
    public static int CharCount(string s) => s.EnumerateRunes().Count();
}
