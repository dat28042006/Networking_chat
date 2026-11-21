namespace ChatApp.Client.Models;

/// <summary>A user as shown in friend lists and message headers.</summary>
public sealed record UserInfo(long Id, string Username, bool Online = false);
