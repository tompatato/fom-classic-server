using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using FOM.Protocol;
using FOM.Protocol.Messages;
using FOM.Server.Capture;

namespace FOM.Server;

/// <summary>
/// The whole game service in one process. It opens a TCP and a UDP listener on
/// every world port in <c>[firstPort, lastPort]</c> (<c>port = 7500 + WorldId</c>),
/// all sharing one dispatcher and one client registry — so all worlds are served
/// by a single instance rather than one process per world.
/// </summary>
public sealed class GameHost
{
    private readonly IPAddress _address;
    private readonly int _firstPort;
    private readonly int _lastPort;
    private readonly ConcurrentDictionary<int, ClientSession> _clients = new();
    private readonly GameDispatcher _dispatcher;
    private readonly CaptureLog _capture;
    private int _connSeq;

    public GameHost(string bindAddress, int firstPort, int lastPort, string? capturePath = null)
    {
        _address = IPAddress.Parse(bindAddress);
        _firstPort = firstPort;
        _lastPort = lastPort;
        _capture = new CaptureLog(capturePath);
        _dispatcher = new GameDispatcher(this);
    }

    /// <summary>Serves every port until <paramref name="ct"/> is cancelled.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var loops = new List<Task>();
        for (int port = _firstPort; port <= _lastPort; port++)
        {
            int p = port;
            loops.Add(Task.Run(() => AcceptLoopAsync(p, ct), ct));
            loops.Add(Task.Run(() => UdpLoopAsync(p, ct), ct));
        }

        PacketLog.Line($"FOM server listening on {_address}:{_firstPort}-{_lastPort} (TCP+UDP, one service)");
        if (_capture.Enabled)
        {
            PacketLog.Line($"capturing session to JSONL");
        }
        _capture.Event("listen", detail: $"{_address}:{_firstPort}-{_lastPort}");
        try
        {
            await Task.WhenAll(loops);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            _capture.Dispose();
        }
    }

    /// <summary>Sends a message to every connected client (used for chat).</summary>
    public async Task BroadcastAsync(IServerMessage message, CancellationToken ct)
    {
        foreach (ClientSession session in _clients.Values)
        {
            try
            {
                await session.SendAsync(message, ct);
            }
            catch
            {
                // a dead peer shouldn't break the broadcast
            }
        }
    }

    private async Task AcceptLoopAsync(int port, CancellationToken ct)
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        try
        {
            listener.Bind(new IPEndPoint(_address, port));
            listener.Listen();
        }
        catch (SocketException e)
        {
            PacketLog.Line($"TCP {port}: bind failed ({e.SocketErrorCode}); skipping");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            Socket connection;
            try
            {
                connection = await listener.AcceptAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }

            connection.NoDelay = true;
            _ = HandleConnectionAsync(connection, port, ct);
        }
    }

    private async Task HandleConnectionAsync(Socket socket, int port, CancellationToken ct)
    {
        int connId = Interlocked.Increment(ref _connSeq);
        var session = new ClientSession(socket, connId, port, _capture);
        _clients[connId] = session;
        PacketLog.Line($"conn#{connId} connected on {session.World}:{port} from {socket.RemoteEndPoint}");
        _capture.Event("connect", connId, port);

        byte[] rent = ArrayPool<byte>.Shared.Rent(4096);
        var frames = new FrameBuffer();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int received;
                try
                {
                    received = await socket.ReceiveAsync(rent, SocketFlags.None, ct);
                }
                catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
                {
                    break;
                }

                if (received == 0)
                {
                    break; // remote closed
                }

                frames.Append(rent.AsSpan(0, received));
                while (frames.TryReadFrame(out ushort opcode, out byte[] body))
                {
                    PacketLog.Packet("C->S", session, opcode, body);
                    bool handled = await _dispatcher.DispatchAsync(session, opcode, body, ct);
                    _capture.Packet("C->S", session, opcode, body, handled);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
            _clients.TryRemove(connId, out _);
            socket.Dispose();
            PacketLog.Line($"conn#{connId} disconnected ({session.World}:{port})");
            _capture.Event("disconnect", connId, port);
        }
    }

    private async Task UdpLoopAsync(int port, CancellationToken ct)
    {
        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udp.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        try
        {
            udp.Bind(new IPEndPoint(_address, port));
        }
        catch (SocketException)
        {
            return; // port unavailable — skip quietly, as the reference does
        }

        byte[] buffer = new byte[65535];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        byte[]? previous = null; // collapse identical consecutive frames (idle position spam)
        while (!ct.IsCancellationRequested)
        {
            SocketReceiveFromResult result;
            try
            {
                result = await udp.ReceiveFromAsync(buffer, SocketFlags.None, remote, ct);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                continue;
            }

            ReadOnlySpan<byte> data = buffer.AsSpan(0, result.ReceivedBytes);
            if (previous is not null && data.SequenceEqual(previous))
            {
                continue;
            }
            previous = data.ToArray();

            ushort opcode = data.Length >= 2 ? (ushort)((data[0] << 8) | data[1]) : (ushort)0;
            MovementUpdate? move = MovementUpdate.TryParse(data);
            _capture.Udp(port, opcode, data, handled: move is not null);
            string suffix = move is { } m
                ? $"  MOVE sess={m.Session} X={m.X} Y={m.Y} Z={m.Z} heading={m.Heading} (~{m.HeadingDegrees:F0}deg)"
                : string.Empty;
            PacketLog.Line($"UDP {WorldPort.FromPort(port)}:{port} <- {result.RemoteEndPoint} len={result.ReceivedBytes}{suffix}");
        }
    }
}
