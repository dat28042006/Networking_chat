using ChatApp.Server;
using ChatApp.Server.Db;

// Entry point. Order matters: database first (fail fast), then wiring, then listen.
await using Database database = Database.FromEnvironment();
if (!await database.PingAsync())
{
    Console.WriteLine("[Server] Cannot reach PostgreSQL. Check CHAT_DB, the firewall and pg_hba.conf.");
    return;
}
Console.WriteLine("[Server] PostgreSQL OK");

int port = 5000;
string? portText = Environment.GetEnvironmentVariable("CHAT_PORT");
if (!string.IsNullOrWhiteSpace(portText) && (!int.TryParse(portText, out port) || port is < 1 or > 65535))
{
    Console.WriteLine("[Server] CHAT_PORT must be a number between 1 and 65535.");
    return;
}

using var sessions = new SessionManager();
await using var server = new TcpServer();
var notifier = new Notifier(server, database);
var context = new ServerContext(database, sessions, notifier);
var dispatcher = new Dispatcher();

SysPingHandler.RegisterOnServer(dispatcher);
new AuthHandlers(context).Register(dispatcher);
new FriendHandlers(context).Register(dispatcher);
new ChatHandlers(context).Register(dispatcher);
new PostHandlers(context).Register(dispatcher);

server.ConnectionLookup = sessions.ConnectionOf;
server.FrameHandler = dispatcher.DispatchAsync;
server.DisconnectHandler = async conn =>
{
    // Only the connection that currently owns the user may mark him offline.
    if (conn.UserId > 0)
        await sessions.UnbindConnectionAsync(conn.UserId, conn);
};
sessions.UserWentOffline += async userId =>
{
    try { await notifier.PresenceChangedAsync(userId, false); }
    catch (Exception ex) { Console.WriteLine($"[Server] presence broadcast failed: {ex.Message}"); }
};

server.Start(port);
Console.WriteLine($"[Server] Listening on TCP {port}. Press Ctrl+C to stop.");

var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.TrySetResult(); };
await stop.Task;

Console.WriteLine("[Server] Stopping...");
await server.StopAsync();
