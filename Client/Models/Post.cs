namespace ChatApp.Client.Models;

/// <summary>A feed post (text + caption only in the core plan).</summary>
public sealed record Post(
    long Id,
    long AuthorId,
    string AuthorName,
    string Caption,
    long TsMs,
    int Reactions,
    int Comments)
{
    public DateTime LocalTime => DateTimeOffset.FromUnixTimeMilliseconds(TsMs).LocalDateTime;
}
