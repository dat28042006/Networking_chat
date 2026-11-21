# Protocol as implemented by the server (Dat/)

Framing: 4-byte big-endian length + UTF-8 JSON, max 1 MiB. Request `{"v":1,"id":N,"type":"...","data":{}}`,
reply `{"v":1,"re":N,"ok":true,"data":{}}` or `{"v":1,"re":N,"ok":false,"error":{"code","message"}}`.
Events: `{"v":1,"type":"evt.*","data":{}}` (no `re`). Times are UTC milliseconds. The field names below are what
`Client/Protocol/ModelParser.cs` reads.

| Request | data in | data out | Errors |
|---|---|---|---|
| sys.ping (no login) | client_ts | type=sys.pong, server_ts, client_ts | |
| auth.register (no login) | username, password | {} | USERNAME_TAKEN, BAD_REQUEST |
| auth.login (no login) | username, password | user{id,username,online}, udp_token (string), udp_port (0), resume_token | INVALID_CREDENTIALS |
| auth.logout | | {} | |
| friend.list | | friends[{id,username,online}] | |
| friend.request | username | {} | USER_NOT_FOUND, ALREADY_FRIENDS, BAD_REQUEST |
| friend.respond | request_id, accept | {} | BAD_REQUEST |
| conv.list | | conversations[{id,name,is_group,owner_id,members[ids]}] | |
| conv.create | name, members[ids] (caller added automatically) | conv_id | NOT_FRIENDS, BAD_REQUEST |
| conv.add_member | conv_id, user_id | {} | NOT_MEMBER, NOT_OWNER, NOT_FRIENDS, USER_NOT_FOUND |
| conv.remove_member | conv_id, user_id | {} | NOT_MEMBER, NOT_OWNER, BAD_REQUEST |
| msg.send | conv_id, body, client_msg_id | msg_id, ts | NOT_MEMBER, BAD_REQUEST |
| msg.history | conv_id, after_id, limit | messages[{id,conv_id,sender_id,sender_name,body,ts,client_msg_id}] | NOT_MEMBER |
| post.create | caption | post_id | BAD_REQUEST |
| post.feed | before_id, limit | posts[{id,author_id,author_name,caption,ts,reactions,comments}] | |

Events: evt.msg (to the other members, not the sender), evt.friend_request {request_id, from}, evt.friend_update,
evt.presence {user_id, online} (to friends), evt.conv_updated {conv_id}, evt.post (to friends).

## Decisions made by the implementation (not in the original plan: please confirm or change)

* Username: 3-32 characters `A-Za-z0-9._-`, case-insensitive unique. Password: 6-128 characters.
* PBKDF2-SHA512, 210,000 iterations, 16-byte salt, 32-byte hash (the schema stores only hash + salt).
* One session per user: a new login closes the older connection.
* Direct chat = two people and no name (re-used if it already exists). Everything else is a group.
* Chats and groups only between accepted friends. Only the group owner adds/removes members; a member may leave;
  the owner can neither leave nor be removed.
* msg.history with after_id = 0 returns the LATEST `limit` messages (oldest first); after_id > 0 returns the next ones.
* Pending friend requests received while offline are pushed once per connection, right after the first friend.list.
* A friend request to someone who already asked you is treated as accepting it.
* Feed = own posts + accepted friends' posts, newest first. Message limit 1000 characters, post limit 2000.
* udp_port is 0 and udp_token is only a placeholder until the UDP media server exists.
