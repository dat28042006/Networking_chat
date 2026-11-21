namespace ChatApp.Client.Models;

/// <summary>Reply to auth.login. The UDP fields are for the media layer (later phase).</summary>
public sealed record LoginResult(UserInfo Me, string UdpToken, int UdpPort, string ResumeToken);
