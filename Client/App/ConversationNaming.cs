using ChatApp.Client.Localization;
using ChatApp.Client.Models;

namespace ChatApp.Client.App;

/// <summary>Decides what to call a conversation: group name, or the other person's name for a direct chat.</summary>
internal static class ConversationNaming
{
    public static string Title(Conversation conversation, UserInfo me, IReadOnlyList<UserInfo> friends)
    {
        if (!string.IsNullOrWhiteSpace(conversation.Name)) return conversation.Name;
        if (!conversation.IsGroup)
        {
            long otherId = conversation.MemberIds.FirstOrDefault(id => id != me.Id);
            UserInfo? other = friends.FirstOrDefault(f => f.Id == otherId);
            if (other is not null) return other.Username;
        }
        return Loc.T("chat.untitled", conversation.Id);
    }

    public static string Subtitle(Conversation conversation, UserInfo me, IReadOnlyList<UserInfo> friends)
    {
        if (conversation.IsGroup) return Loc.T("chat.members", conversation.MemberIds.Count);
        bool? online = OtherOnline(conversation, me, friends);
        return online is null ? "" : online.Value ? Loc.T("friends.online") : Loc.T("friends.offline");
    }

    /// <summary>null for group chats or unknown users.</summary>
    public static bool? OtherOnline(Conversation conversation, UserInfo me, IReadOnlyList<UserInfo> friends)
    {
        if (conversation.IsGroup) return null;
        long otherId = conversation.MemberIds.FirstOrDefault(id => id != me.Id);
        UserInfo? other = friends.FirstOrDefault(f => f.Id == otherId);
        return other?.Online;
    }
}
