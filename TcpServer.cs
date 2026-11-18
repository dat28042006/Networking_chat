using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ChatApp.Server
{
    /// <summary>
    /// Đại diện cho một kết nối TCP của client với framing 4-byte big-endian + UTF-8 JSON
    /// và tự động ngắt kết nối nếu im lặng quá 45 giây.
    /// </summary>
    public sealed class ClientConnection : IAsyncDisposable
    {
        public const int MaxFrameSize = 1024 * 1024; // 1 MiB max payload
        public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(45);

        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private long _lastActivityUtcMs;

        public long UserId { get; set; }
        public bool IsAuthenticated => UserId > 0;
        public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

        public event Func<ClientConnection, JsonObject, Task>? FrameReceived;
        public event Func<ClientConnection, Task>? Disconnected;

        public ClientConnection(TcpClient client)
        {
            _client = client;
            _client.NoDelay = true;
            _stream = client.GetStream();
            _lastActivityUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void StartProcessing()
        {
            _ = Task.Run(ReceiveLoopAsync);
            _ = Task.Run(IdleWatchdogAsync);
        }

        public async Task SendAsync(JsonObject payload, CancellationToken ct = default)
        {
            if (!_client.Connected) return;

            byte[] jsonBytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
            if (jsonBytes.Length > MaxFrameSize) return;

            byte[] frame = new byte[4 + jsonBytes.Length];
            BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, 4), (uint)jsonBytes.Length);
            jsonBytes.CopyTo(frame, 4);

            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _stream.WriteAsync(frame, ct).ConfigureAwait(false);
                await _stream.FlushAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task CloseWithErrorAsync(string code, string message)
        {
            var errObj = new JsonObject
            {
                ["v"] = 1,
                ["ok"] = false,
                ["error"] = new JsonObject
                {
                    ["code"] = code,
                    ["message"] = message
                }
            };
            await SendAsync(errObj).ConfigureAwait(false);
            await CloseAsync().ConfigureAwait(false);
        }

        private async Task ReceiveLoopAsync()
        {
            byte[] headerBuf = new byte[4];
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (!await ReadExactAsync(headerBuf, 4, _cts.Token).ConfigureAwait(false))
                        break;

                    _lastActivityUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    uint frameLen = BinaryPrimitives.ReadUInt32BigEndian(headerBuf);

                    if (frameLen > MaxFrameSize)
                    {
                        await CloseWithErrorAsync("BAD_REQUEST", "Frame exceeds 1 MiB limit").ConfigureAwait(false);
                        break;
                    }

                    byte[] payloadBuf = new byte[frameLen];
                    if (!await ReadExactAsync(payloadBuf, (int)frameLen, _cts.Token).ConfigureAwait(false))
                        break;

                    _lastActivityUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    JsonNode? node;
                    try
                    {
                        node = JsonNode.Parse(payloadBuf);
                    }
                    catch (JsonException)
                    {
                        await CloseWithErrorAsync("BAD_REQUEST", "Malformed JSON frame").ConfigureAwait(false);
                        break;
                    }

                    if (node is not JsonObject jsonObj)
                    {
                        await CloseWithErrorAsync("BAD_REQUEST", "Root JSON must be an object").ConfigureAwait(false);
                        break;
                    }

                    if (FrameReceived != null)
                        await FrameReceived.Invoke(this, jsonObj).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                await CloseAsync().ConfigureAwait(false);
            }
        }

        private async Task<bool> ReadExactAsync(byte[] buffer, int count, CancellationToken ct)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await _stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct).ConfigureAwait(false);
                if (read == 0) return false; // EOF
                offset += read;
            }
            return true;
        }

        private async Task IdleWatchdogAsync()
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
                while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (now - _lastActivityUtcMs > IdleTimeout.TotalMilliseconds)
                    {
                        await CloseAsync().ConfigureAwait(false);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public async Task CloseAsync()
        {
            if (_cts.IsCancellationRequested) return;
            _cts.Cancel();

            try { _client.Close(); } catch { }

            if (Disconnected != null)
                await Disconnected.Invoke(this).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync().ConfigureAwait(false);
            _sendLock.Dispose();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// TCP Server: lắng nghe kết nối (cổng mặc định 5000), quản lý các ClientConnection
    /// và phát sự kiện tới danh sách UserId.
    /// </summary>
    public sealed class TcpServer : IAsyncDisposable
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly ConcurrentDictionary<ClientConnection, byte> _connections = new();

        public Func<ClientConnection, JsonObject, Task>? FrameHandler { get; set; }
        public Func<ClientConnection, Task>? DisconnectHandler { get; set; }
        public Func<long, ClientConnection?>? ConnectionLookup { get; set; }

        public bool Start(int port = 5000)
        {
            if (_listener != null) return false;

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
            return true;
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                try
                {
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                    var conn = new ClientConnection(tcpClient);
                    _connections.TryAdd(conn, 0);

                    conn.FrameReceived += async (c, frame) =>
                    {
                        if (FrameHandler != null)
                            await FrameHandler(c, frame).ConfigureAwait(false);
                    };

                    conn.Disconnected += async c =>
                    {
                        _connections.TryRemove(c, out _);
                        if (DisconnectHandler != null)
                            await DisconnectHandler(c).ConfigureAwait(false);
                    };

                    conn.StartProcessing();
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        public async Task BroadcastToAsync(IEnumerable<long> userIds, JsonObject evt, CancellationToken ct = default)
        {
            if (ConnectionLookup == null) return;
            foreach (long uid in userIds)
            {
                var conn = ConnectionLookup(uid);
                if (conn != null)
                    await conn.SendAsync(evt, ct).ConfigureAwait(false);
            }
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener = null;

            foreach (var conn in _connections.Keys)
                await conn.DisposeAsync().ConfigureAwait(false);

            _connections.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _cts?.Dispose();
        }
    }
}