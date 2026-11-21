namespace ChatApp.Client.Models;

/// <summary>A failed request or a network problem. Code matches the server's error.code.</summary>
public sealed record ApiError(string Code, string Message, long RequestId);
