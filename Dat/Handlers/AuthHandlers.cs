using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using ChatApp.Server.Db;

namespace ChatApp.Server;

/// <summary>auth.register, auth.login, auth.logout.</summary>
public sealed class AuthHandlers
{
    // PBKDF2 settings are fixed (the schema stores only hash + salt). Changing them invalidates existing passwords.
    private const int Pbkdf2Iterations = 210_000;
    private const int HashBytes = 32;
    private const int SaltBytes = 16;
    private static readonly Regex UsernameRule = new("^[A-Za-z0-9._-]{3,32}$", RegexOptions.Compiled);
    private static readonly byte[] DummySalt = new byte[SaltBytes];

    private readonly ServerContext _ctx;

    public AuthHandlers(ServerContext ctx) => _ctx = ctx;

    public void Register(Dispatcher dispatcher)
    {
        dispatcher.RegisterHandler(MessageTypes.AuthRegister, RegisterAsync, requiresAuth: false);
        dispatcher.RegisterHandler(MessageTypes.AuthLogin, LoginAsync, requiresAuth: false);
        dispatcher.RegisterHandler(MessageTypes.AuthLogout, LogoutAsync, requiresAuth: true);
    }

    private static byte[] Hash(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA512, HashBytes);

    private async Task<JsonObject> RegisterAsync(ClientConnection conn, JsonObject data)
    {
        string username = Req.Str(data, "username", 64).Trim();
        string password = Req.Str(data, "password", 128);

        if (!UsernameRule.IsMatch(username))
            throw ApiException.BadRequest("Username must be 3-32 characters: letters, digits, '.', '_' or '-'.");
        if (password.Length < 6)
            throw ApiException.BadRequest("Password must be at least 6 characters.");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = await Task.Run(() => Hash(password, salt)).ConfigureAwait(false);

        long? id = await _ctx.Db.CreateUserAsync(username, hash, salt).ConfigureAwait(false);
        if (id is null) throw new ApiException("USERNAME_TAKEN", "Username already exists.");
        return Dispatcher.MakeSuccess();
    }

    private async Task<JsonObject> LoginAsync(ClientConnection conn, JsonObject data)
    {
        if (conn.IsAuthenticated) throw ApiException.BadRequest("Already logged in on this connection.");

        string username = Req.Str(data, "username", 64).Trim();
        string password = Req.Str(data, "password", 128);

        UserRow? user = await _ctx.Db.GetUserByNameAsync(username).ConfigureAwait(false);

        // Hash even for unknown users so the reply time does not reveal which usernames exist.
        byte[] salt = user?.Salt ?? DummySalt;
        byte[] hash = await Task.Run(() => Hash(password, salt)).ConfigureAwait(false);
        if (user is null || !CryptographicOperations.FixedTimeEquals(hash, user.PassHash))
            throw new ApiException("INVALID_CREDENTIALS", "Wrong username or password.");

        // One session per user: the newest login wins. Bind first, then close the old connection,
        // so the old connection's disconnect does not report the user as offline.
        ClientConnection? previous = _ctx.Sessions.ConnectionOf(user.Id);
        conn.UserId = user.Id;
        conn.Username = user.Username;
        _ctx.Sessions.BindConnection(user.Id, conn);
        if (previous is not null && !ReferenceEquals(previous, conn))
            _ = previous.CloseWithErrorAsync("SESSION_REPLACED", "You signed in from another place.");

        ulong udpToken = _ctx.Sessions.IssueUdpToken(user.Id);
        string resumeToken = _ctx.Sessions.IssueResumeToken(user.Id, udpToken);

        conn.AfterReply.Enqueue(() => _ctx.Notify.PresenceChangedAsync(user.Id, true));

        return Dispatcher.MakeSuccess(0, new JsonObject
        {
            ["user"] = Wire.User(user.Id, user.Username, true),
            ["udp_token"] = udpToken.ToString(),
            ["udp_port"] = 0,                 // 0 = no UDP media server yet
            ["resume_token"] = resumeToken
        });
    }

    private async Task<JsonObject> LogoutAsync(ClientConnection conn, JsonObject data)
    {
        long userId = conn.UserId;
        await _ctx.Sessions.UnbindConnectionAsync(userId, conn).ConfigureAwait(false); // raises UserWentOffline
        conn.UserId = 0;
        conn.Username = "";
        conn.PendingRequestsSent = false;
        return Dispatcher.MakeSuccess();
    }
}
