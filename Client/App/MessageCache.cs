using ChatApp.Client.Models;

namespace ChatApp.Client.App;

/// <summary>
/// Keeps every message we have seen, per conversation, sorted and de-duplicated by message id.
/// History replies, live events and our own send-acks can overlap: the cache makes that harmless.
/// </summary>
internal sealed class MessageCache
{
    private readonly Dictionary<long, SortedList<long, ChatMessage>> _byConversation = new();

    /// <returns>true if the message was new.</returns>
    public bool Add(ChatMessage message)
    {
        SortedList<long, ChatMessage> list = GetList(message.ConvId);
        if (list.ContainsKey(message.Id)) return false;
        list.Add(message.Id, message);
        return true;
    }

    public void Merge(IEnumerable<ChatMessage> messages)
    {
        foreach (ChatMessage message in messages) Add(message);
    }

    public IReadOnlyList<ChatMessage> Get(long conversationId) => GetList(conversationId).Values.ToList();

    /// <summary>Highest message id known (used as after_id for msg.history). 0 if none.</summary>
    public long LastId(long conversationId)
    {
        SortedList<long, ChatMessage> list = GetList(conversationId);
        return list.Count == 0 ? 0 : list.Keys[list.Count - 1];
    }

    private SortedList<long, ChatMessage> GetList(long conversationId)
    {
        if (!_byConversation.TryGetValue(conversationId, out SortedList<long, ChatMessage>? list))
        {
            list = new SortedList<long, ChatMessage>();
            _byConversation[conversationId] = list;
        }
        return list;
    }
}
