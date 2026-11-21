namespace ChatApp.Client.Models;

/// <summary>A direct chat (two members) or a group chat. For direct chats Name may be empty.</summary>
public sealed record Conversation(
    long Id,
    string Name,
    bool IsGroup,
    long OwnerId,
    IReadOnlyList<long> MemberIds);
