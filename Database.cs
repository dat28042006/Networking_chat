using Npgsql;

namespace chat;

public class Database
{
    private readonly string _connString =
        "Host=localhost;Port=5433;Database=Atlas_dev;Username=postgres;Password=dat123";

    public NpgsqlConnection GetConnection()
    {
        return new NpgsqlConnection(_connString);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();
            Console.WriteLine("[DB] Ket noi PostgreSQL thanh cong!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB ERROR] {ex.Message}");
            return false;
        }
    }
}
