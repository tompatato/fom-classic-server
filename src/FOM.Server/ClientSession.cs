using System.Net.Sockets;
using FOM.Protocol;
using FOM.Protocol.Messages;

namespace FOM.Server;

/// <summary>
/// One connected TCP client. Wraps the socket so every send is framed and logged,
/// and serializes concurrent sends (chat broadcasts arrive from other sessions).
/// </summary>
public sealed class ClientSession(Socket socket, int connId, int port)
{
    private readonly Socket _socket = socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public int ConnId { get; } = connId;
    public int Port { get; } = port;
    public WorldId World => WorldPort.FromPort(Port);

    /// <summary>Set from the login username; used as the chat display name.</summary>
    public string Name { get; set; } = "Player";

    public async Task SendAsync(IServerMessage message, CancellationToken ct = default)
    {
        byte[] frame = message.ToFrame();
        PacketLog.Packet("S->C", this, (ushort)message.Id, frame.AsSpan(PacketFrame.HeaderSize));

        await _sendLock.WaitAsync(ct);
        try
        {
            int sent = 0;
            while (sent < frame.Length)
            {
                sent += await _socket.SendAsync(frame.AsMemory(sent), SocketFlags.None, ct);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
