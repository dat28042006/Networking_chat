namespace ChatApp.Server;

/// <summary>Every "type" string of the TCP protocol (mirror of Client/Protocol/MessageTypes.cs).</summary>
public static class MessageTypes
{
    public const string AuthRegister = "auth.register";
    public const string AuthLogin = "auth.login";
    public const string AuthLogout = "auth.logout";

    public const string MsgSend = "msg.send";
    public const string MsgHistory = "msg.history";

    public const string FriendRequest = "friend.request";
    public const string FriendRespond = "friend.respond";
    public const string FriendList = "friend.list";

    public const string ConvList = "conv.list";
    public const string ConvCreate = "conv.create";
    public const string ConvAddMember = "conv.add_member";
    public const string ConvRemoveMember = "conv.remove_member";

    public const string PostCreate = "post.create";
    public const string PostFeed = "post.feed";

    // Events pushed by the server
    public const string EvtMsg = "evt.msg";
    public const string EvtFriendRequest = "evt.friend_request";
    public const string EvtFriendUpdate = "evt.friend_update";
    public const string EvtPresence = "evt.presence";
    public const string EvtConvUpdated = "evt.conv_updated";
    public const string EvtPost = "evt.post";
}
