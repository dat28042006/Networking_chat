using System.Text.Json.Nodes;

namespace ChatApp.Client.Protocol;

/// <summary>Builds request envelopes: {"v":1,"id":N,"type":"...","data":{...}}.</summary>
public static class Envelope
{
    public const int Version = 1;

    public static JsonObject Request(string type, long id, JsonObject? data = null) => new()
    {
        ["v"] = Version,
        ["id"] = id,
        ["type"] = type,
        ["data"] = data ?? new JsonObject()
    };
}
