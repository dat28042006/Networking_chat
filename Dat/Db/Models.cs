namespace ChatApp.Server.Db;

public sealed record UserRow(long Id, string Username, byte[] PassHash, byte[] Salt);
public sealed record UserBrief(long Id, string Username);
public sealed record ConvRow(long Id, string? Name, bool IsGroup, long? OwnerId, long[] Members);
public sealed record MessageRow(long Id, long ConvId, long SenderId, string SenderName, string Body, long TsMs, string? ClientMsgId);
public sealed record PostRow(long Id, long AuthorId, string AuthorName, string Caption, long TsMs, long Reactions, long Comments);
public sealed record PendingRequestRow(long RequestId, long FromId, string FromName);

public enum FriendRequestResult { Created, AlreadyFriends, AlreadyPending, AcceptedMutual }
