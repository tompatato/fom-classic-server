using System.Net.Sockets;
using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.TestClient;

/// <summary>
/// A scriptable, headless stand-in for the game client: connects over TCP, sends
/// client→server packets, and reads framed responses. Lets tests (and tooling)
/// drive real login/world/chat/ping sequences against the server without the game.
/// </summary>
public sealed class ScenarioClient : IAsyncDisposable
{
    private readonly FrameBuffer _frames = new();
    private TcpClient? _tcp;
    private NetworkStream? _stream;

    /// <summary>Connects, retrying briefly so a just-started server isn't a race.</summary>
    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        while (true)
        {
            var tcp = new TcpClient();
            try
            {
                await tcp.ConnectAsync(host, port, ct);
                tcp.NoDelay = true;
                _tcp = tcp;
                _stream = tcp.GetStream();
                return;
            }
            catch (SocketException)
            {
                tcp.Dispose();
                await Task.Delay(50, ct); // listener not up yet
            }
        }
    }

    private NetworkStream Stream => _stream ?? throw new InvalidOperationException("not connected");

    // --- client→server sends ---

    public Task SendLoginAsync(string name, CancellationToken ct = default) =>
        SendFrameAsync(new PacketWriter().WriteFixedCString(name, 20).ToFrame(PacketId.LOGIN_REQUEST), ct);

    public Task SendLoadCharAsync(CancellationToken ct = default) =>
        SendFrameAsync(PacketFrame.Encode(PacketId.LOAD_CHAR, []), ct);

    public Task SendExitAptAsync(CancellationToken ct = default) =>
        SendFrameAsync(PacketFrame.Encode(PacketId.EXIT_APT, []), ct);

    public Task SendPingAsync(uint timestamp, CancellationToken ct = default) =>
        SendFrameAsync(new PacketWriter().WriteU32(timestamp).ToFrame(PacketId.PING), ct);

    /// <summary>Sends a chat packet (same body layout the real client sends).</summary>
    public Task SendChatAsync(uint senderId, ushort channel, string name, string message, CancellationToken ct = default) =>
        SendFrameAsync(new ChatBroadcast(senderId, channel, name, message).ToFrame(), ct);

    public Task SendRawAsync(PacketId opcode, ReadOnlySpan<byte> body, CancellationToken ct = default) =>
        SendFrameAsync(PacketFrame.Encode(opcode, body), ct);

    private Task SendFrameAsync(byte[] frame, CancellationToken ct) => Stream.WriteAsync(frame, ct).AsTask();

    // --- server→client reads ---

    /// <summary>Reads the next complete frame, awaiting more bytes as needed.</summary>
    public async Task<(PacketId Opcode, byte[] Body)> ReadFrameAsync(CancellationToken ct = default)
    {
        byte[] buffer = new byte[4096];
        while (true)
        {
            if (_frames.TryReadFrame(out ushort opcode, out byte[] body))
            {
                return ((PacketId)opcode, body);
            }

            int read = await Stream.ReadAsync(buffer, ct);
            if (read == 0)
            {
                throw new EndOfStreamException("server closed the connection");
            }
            _frames.Append(buffer.AsSpan(0, read));
        }
    }

    /// <summary>Reads frames until one with <paramref name="expected"/> arrives, returning its body.</summary>
    public async Task<byte[]> ExpectAsync(PacketId expected, CancellationToken ct = default)
    {
        while (true)
        {
            (PacketId opcode, byte[] body) = await ReadFrameAsync(ct);
            if (opcode == expected)
            {
                return body;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync();
        }
        _tcp?.Dispose();
    }
}
