namespace ChatApp.Client.Models;

/// <summary>One chat message (matches the evt.msg payload). TsMs is UTC Unix milliseconds.</summary>
public sealed record ChatMessage(
    long Id,
    long ConvId,
    long SenderId,
    string SenderName,
    string Body,
    long TsMs,
    string? ClientMsgId = null)
{
    public DateTime LocalTime => DateTimeOffset.FromUnixTimeMilliseconds(TsMs).LocalDateTime;
}
