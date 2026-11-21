using System.Text.Json.Nodes;
using ChatApp.Client.Models;

namespace ChatApp.Client.Protocol;

/// <summary>
/// JSON -> model conversion. Lenient on purpose (missing fields become defaults) so a small
/// server difference never crashes the UI. Field names are listed in docs/PROTOCOL_ASSUMPTIONS.md.
/// If a list is empty or a name shows as blank in the UI, the cause is usually a field name here.
/// </summary>
public static class ModelParser
{
    public static long L(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<long>(out long number)) return number;
            if (value.TryGetValue<string>(out string? text) && long.TryParse(text, out long parsed)) return parsed;
        }
        return 0;
    }

    public static bool B(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out bool flag)) return flag;
            if (value.TryGetValue<string>(out string? text) && bool.TryParse(text, out bool parsed)) return parsed;
        }
        return false;
    }

    public static string S(JsonNode? node) => node?.ToString() ?? "";

    public static List<T> ParseList<T>(JsonNode? node, Func<JsonNode?, T> map)
    {
        var list = new List<T>();
        if (node is JsonArray array)
            foreach (JsonNode? item in array)
                list.Add(map(item));
        return list;
    }

    public static UserInfo ParseUser(JsonNode? node)
    {
        if (node is not JsonObject o) return new UserInfo(0, "?");
        return new UserInfo(L(o["id"]), S(o["username"]), B(o["online"]));
    }

    public static ChatMessage ParseMessage(JsonNode? node)
    {
        JsonObject o = node as JsonObject ?? new JsonObject();
        return new ChatMessage(
            L(o["id"]), L(o["conv_id"]), L(o["sender_id"]),
            S(o["sender_name"]), S(o["body"]), L(o["ts"]),
            o["client_msg_id"]?.ToString());
    }

    public static Conversation ParseConversation(JsonNode? node)
    {
        JsonObject o = node as JsonObject ?? new JsonObject();
        return new Conversation(
            L(o["id"]), S(o["name"]), B(o["is_group"]), L(o["owner_id"]),
            ParseList(o["members"], n => L(n)));
    }

    public static Post ParsePost(JsonNode? node)
    {
        JsonObject o = node as JsonObject ?? new JsonObject();
        return new Post(
            L(o["id"]), L(o["author_id"]), S(o["author_name"]), S(o["caption"]),
            L(o["ts"]), (int)L(o["reactions"]), (int)L(o["comments"]));
    }

    public static LoginResult ParseLogin(JsonObject data) => new(
        ParseUser(data["user"]),
        S(data["udp_token"]),
        (int)L(data["udp_port"]),
        S(data["resume_token"]));
}
