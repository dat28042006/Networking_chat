using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ChatApp.Server
{
    /// <summary>
    /// Xử lý heartbeat hệ thống (`sys.ping` -> `sys.pong`).
    /// Protocol: Client gửi `sys.ping` mỗi 15s; Server ngắt kết nối nếu im lặng quá 45s.
    /// </summary>
    public static class SysPingHandler
    {
        public const string TypePing = "sys.ping";
        public const string TypePong = "sys.pong";
        public static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan ServerTimeout = TimeSpan.FromSeconds(45);

        /// <summary>
        /// Đăng ký handler `sys.ping` lên Dispatcher của server (không yêu cầu đăng nhập trước).
        /// </summary>
        public static void RegisterOnServer(Dispatcher dispatcher)
        {
            dispatcher.RegisterHandler(
                TypePing,
                (conn, data) =>
                {
                    long serverTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var respData = new JsonObject
                    {
                        ["type"] = TypePong,
                        ["server_ts"] = serverTs
                    };

                    if (data.TryGetPropertyValue("client_ts", out var clientTsNode) && clientTsNode != null)
                        respData["client_ts"] = clientTsNode.DeepClone();

                    return Task.FromResult(new JsonObject
                    {
                        ["ok"] = true,
                        ["data"] = respData
                    });
                },
                requiresAuth: false
            );
        }
    }

    /// <summary>
    /// Bộ đếm heartbeat phía Client: tự động gửi `sys.ping` mỗi 15 giây khi đang kết nối.
    /// </summary>
    public sealed class HeartbeatTimer : IDisposable
    {
        private readonly Func<string, JsonObject, Task> _sendPingAsync;
        private CancellationTokenSource? _cts;

        public HeartbeatTimer(Func<string, JsonObject, Task> sendPingAsync)
        {
            _sendPingAsync = sendPingAsync;
        }

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => LoopAsync(_cts.Token));
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(SysPingHandler.PingInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    var payload = new JsonObject
                    {
                        ["client_ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    await _sendPingAsync(SysPingHandler.TypePing, payload).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose() => Stop();
    }
}