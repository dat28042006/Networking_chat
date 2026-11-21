# Connecting PostgreSQL to the ChatApp server (Visual Studio)

## 0. Who connects to what

```
WinForms client (Developer B)  --TCP 5000-->  C# server  --Npgsql, port 5432-->  PostgreSQL
                                              (Developer A's PC)                  (Developer A's PC)
```

* **Only the server talks to PostgreSQL.** The client never connects to the database.
* So Developer B does **not** need database access to run or test the app: start the client, untick
  "Demo mode", and connect to `<A's IP>:5000`. B only needs PostgreSQL access to *look at* the data (section 5).
* If the server and PostgreSQL both run on A's PC, the connection string uses `Host=localhost` and
  PostgreSQL does **not** have to be opened to the network at all (skip section 2).

## 1. Create the database and a dedicated login (on A's PC)

Open **pgAdmin 4** (installed with PostgreSQL) -> *Tools -> Query Tool* on the `postgres` database, or use `psql`:

```sql
CREATE ROLE chatapp_user LOGIN PASSWORD 'choose-a-long-password';
CREATE DATABASE chatapp OWNER chatapp_user;
```

Then connect to the new `chatapp` database and run **`postgres_schema.sql`**:
* a fresh database: PART 1,
* the existing database built from the old plan: PART 2 (adds `messages.client_msg_id`, the case-insensitive
  username index, a message length check and a few indexes; safe to re-run).

## 2. Let other PCs on the LAN reach PostgreSQL (only if needed)

Needed only if someone other than A wants to open the database (for example B using pgAdmin) or runs the server on another PC.

1. Find A's IP: `ipconfig` -> *IPv4 Address*, for example `192.168.1.20`.
2. Edit `C:\Program Files\PostgreSQL\<version>\data\postgresql.conf`:
   `listen_addresses = '*'` (default is `localhost`).
3. Edit `pg_hba.conf` in the same folder, add one line (use your own subnet):
   ```
   host    chatapp    chatapp_user    192.168.1.0/24    scram-sha-256
   ```
4. Restart the service: `services.msc` -> **postgresql-x64-<version>** -> Restart.
5. Windows Firewall (PowerShell as administrator), private networks only:
   ```powershell
   New-NetFirewallRule -DisplayName "PostgreSQL (LAN)" -Direction Inbound -Protocol TCP -LocalPort 5432 -Action Allow -Profile Private -RemoteAddress 192.168.1.0/24
   ```
6. Test from the other PC: `Test-NetConnection 192.168.1.20 -Port 5432` must show `TcpTestSucceeded : True`.

Never forward port 5432 on the router and never open it to the internet.

## 3. Add Npgsql to the server project (Visual Studio)

1. Solution Explorer -> right-click the **chat** project (server) -> **Manage NuGet Packages...**
2. *Browse* tab -> search **Npgsql** -> pick the newest stable version that supports .NET 8 -> **Install**.
   Do not use 8.0.0-8.0.2: they have a published high-severity security advisory (GHSA-x9vc-6hfv-hg8c).
3. Same thing from a terminal: `dotnet add TcpServer/chat/chat.csproj package Npgsql` (no version = latest).

## 4. Connection string and code

**Store the connection string in an environment variable (never in Git).** On each machine that runs the server
(PowerShell), then **restart Visual Studio**:

```powershell
setx CHAT_DB "Host=localhost;Port=5432;Database=chatapp;Username=chatapp_user;Password=your-password"
```

Use `Host=192.168.1.20` instead of `localhost` when the server runs on a different PC than PostgreSQL.

Add `TcpServer/chat/Db/Database.cs`:

```csharp
using Npgsql;

namespace ChatApp.Server.Db;

public sealed class Database : IAsyncDisposable
{
    private readonly NpgsqlDataSource _db;

    public Database(string connectionString) => _db = NpgsqlDataSource.Create(connectionString);

    public static Database FromEnvironment()
    {
        string? cs = Environment.GetEnvironmentVariable("CHAT_DB");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Environment variable CHAT_DB is not set (see docs/POSTGRESQL_SETUP.md).");
        return new Database(cs);
    }

    /// <summary>Health check used at server start.</summary>
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            await using NpgsqlCommand cmd = _db.CreateCommand("SELECT 1");
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] ping failed: {ex.Message}");
            return false;
        }
    }

    /// <returns>The new user id, or null if the username is already taken.</returns>
    public async Task<long?> CreateUserAsync(string username, byte[] passHash, byte[] salt, CancellationToken ct = default)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "INSERT INTO users (username, pass_hash, salt) VALUES (@u, @h, @s) ON CONFLICT DO NOTHING RETURNING id");
        cmd.Parameters.AddWithValue("u", username);
        cmd.Parameters.AddWithValue("h", passHash);
        cmd.Parameters.AddWithValue("s", salt);
        object? id = await cmd.ExecuteScalarAsync(ct);
        return id is long value ? value : null;
    }

    /// <summary>Stores a message. A repeated clientMsgId returns the ORIGINAL id and timestamp.</summary>
    public async Task<(long Id, long TsMs)> AppendMessageAsync(
        long convId, long senderId, string? clientMsgId, string body, long tsMs, CancellationToken ct = default)
    {
        await using (NpgsqlCommand insert = _db.CreateCommand(
            "INSERT INTO messages (conv_id, sender_id, client_msg_id, body, ts_ms) " +
            "VALUES (@c, @s, @m, @b, @t) ON CONFLICT (sender_id, client_msg_id) DO NOTHING RETURNING id"))
        {
            insert.Parameters.AddWithValue("c", convId);
            insert.Parameters.AddWithValue("s", senderId);
            insert.Parameters.AddWithValue("m", (object?)clientMsgId ?? DBNull.Value);
            insert.Parameters.AddWithValue("b", body);
            insert.Parameters.AddWithValue("t", tsMs);
            if (await insert.ExecuteScalarAsync(ct) is long newId) return (newId, tsMs);
        }

        // Duplicate send: return the stored message so the client gets the same ack.
        await using NpgsqlCommand existing = _db.CreateCommand(
            "SELECT id, ts_ms FROM messages WHERE sender_id = @s AND client_msg_id = @m");
        existing.Parameters.AddWithValue("s", senderId);
        existing.Parameters.AddWithValue("m", clientMsgId ?? "");
        await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct)) return (reader.GetInt64(0), reader.GetInt64(1));
        throw new InvalidOperationException("Message insert failed.");
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
```

Check it at server start, in `Program.cs` before the server starts listening:

```csharp
await using var database = ChatApp.Server.Db.Database.FromEnvironment();
if (!await database.PingAsync())
{
    Console.WriteLine("[Server] Cannot reach PostgreSQL. Check CHAT_DB, the firewall and pg_hba.conf.");
    return;
}
Console.WriteLine("[Server] PostgreSQL OK");
```

Rules that keep the database safe: always use parameters (`@u`, never string concatenation); hash passwords with
`Rfc2898DeriveBytes.Pbkdf2` and a random per-user salt, compare with `CryptographicOperations.FixedTimeEquals`;
create one `NpgsqlDataSource` for the whole server (it pools connections).

## 5. Looking at the data from your PC

* **pgAdmin 4** (easiest): *Servers -> Register -> Server*, Connection tab: Host = A's IP, Port 5432,
  Database `chatapp`, Username `chatapp_user`. Needs section 2 done.
* **Visual Studio itself**: Server Explorer has no built-in PostgreSQL provider. If you want it inside Visual Studio,
  install the **psqlODBC** driver, create a 64-bit ODBC DSN, then *Server Explorer -> Connect to Database ->
  Microsoft ODBC Data Source*. pgAdmin is smoother.

## 6. If it does not connect

| Message | Cause / fix |
|---|---|
| Timeout / connection refused | `listen_addresses`, service not restarted, or firewall rule missing (section 2) |
| `no pg_hba.conf entry for host ...` | Add or fix the `host` line in `pg_hba.conf`, restart the service |
| `password authentication failed` | Wrong password or role name; reset with `ALTER ROLE chatapp_user PASSWORD '...';` |
| `database "chatapp" does not exist` | Run the `CREATE DATABASE` step |
| `relation "messages" does not exist` | Run `postgres_schema.sql` on the **chatapp** database |
| SSL error on the LAN | For local development only, add `SSL Mode=Disable` to the connection string |
| `CHAT_DB is not set` | Run `setx` again and restart Visual Studio (it reads variables at start) |
