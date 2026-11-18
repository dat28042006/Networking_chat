using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ChatApp.Server;

// 1. Khởi tạo các thành phần lõi
using var sessionManager = new SessionManager();
var dispatcher = new Dispatcher();
await using var tcpServer = new TcpServer();

// 2. Đăng ký handler sys.ping (và các handler auth.*, msg.* sau này)
SysPingHandler.RegisterOnServer(dispatcher);

// 3. Kết nối TcpServer với Dispatcher và SessionManager
tcpServer.ConnectionLookup = userId => sessionManager.ConnectionOf(userId);
tcpServer.FrameHandler = async (conn, frame) =>
{
    await dispatcher.DispatchAsync(conn, frame);
};
tcpServer.DisconnectHandler = async conn =>
{
    if (conn.IsAuthenticated)
        await sessionManager.UnbindConnectionAsync(conn.UserId, conn);
    Console.WriteLine($"[Server] Client disconnected: {conn.RemoteEndPoint}");
};

// 4. Bật Server ở cổng 5000
tcpServer.Start(5000);
Console.WriteLine("[Server] Listening on port 5000...");

// --- ĐOẠN TEST NHANH (Giả lập 1 Client kết nối và gửi sys.ping) ---
await Task.Delay(500);
await RunQuickTestClientAsync();

Console.WriteLine("Nhan Enter de dung server...");
Console.ReadLine();

static async Task RunQuickTestClientAsync()
{
    using var client = new TcpClient();
    await client.ConnectAsync("127.0.0.1", 5000);
    await using var stream = client.GetStream();

    // Tạo gói tin sys.ping đúng chuẩn envelope v=1
    var pingReq = new JsonObject
    {
        ["v"] = 1,
        ["id"] = 1,
        ["type"] = "sys.ping",
        ["data"] = new JsonObject
        {
            ["client_ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }
    };

    byte[] payload = Encoding.UTF8.GetBytes(pingReq.ToJsonString());
    byte[] frame = new byte[4 + payload.Length];
    BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, 4), (uint)payload.Length);
    payload.CopyTo(frame, 4);

    // Gửi ping lên server
    await stream.WriteAsync(frame);
    Console.WriteLine($"[Client -> Server] {pingReq.ToJsonString()}");

    // Đọc 4-byte độ dài + JSON phản hồi sys.pong từ server
    byte[] lenBuf = new byte[4];
    await stream.ReadExactlyAsync(lenBuf);
    int respLen = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuf);

    byte[] respBuf = new byte[respLen];
    await stream.ReadExactlyAsync(respBuf);
    Console.WriteLine($"[Server -> Client] {Encoding.UTF8.GetString(respBuf)}");
}