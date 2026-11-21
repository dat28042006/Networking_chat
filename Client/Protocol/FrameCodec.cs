using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace ChatApp.Client.Protocol;

/// <summary>
/// Wire framing, identical to the server: [4-byte big-endian length][UTF-8 JSON], max 1 MiB.
/// If the client and server ever disagree about bytes on the wire, this is the only file to check.
/// </summary>
public static class FrameCodec
{
    public const int MaxFrameSize = 1024 * 1024;

    public static async Task WriteFrameAsync(Stream stream, JsonObject payload, CancellationToken ct)
    {
        byte[] json = Encoding.UTF8.GetBytes(payload.ToJsonString());
        if (json.Length > MaxFrameSize)
            throw new InvalidDataException("Frame exceeds 1 MiB.");

        byte[] frame = new byte[4 + json.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, 4), (uint)json.Length);
        json.CopyTo(frame, 4);

        await stream.WriteAsync(frame, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Returns null when the server closed the connection.</summary>
    public static async Task<JsonObject?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        byte[] header = new byte[4];
        if (!await ReadExactAsync(stream, header, ct).ConfigureAwait(false))
            return null;

        uint length = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (length > MaxFrameSize)
            throw new InvalidDataException("Server sent a frame larger than 1 MiB.");

        byte[] payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, ct).ConfigureAwait(false))
            return null;

        JsonNode? node = JsonNode.Parse(payload);
        return node as JsonObject ?? throw new InvalidDataException("Frame root must be a JSON object.");
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), ct).ConfigureAwait(false);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }
}
