using System.Buffers;
using System.Buffers.Binary;
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
    private readonly WorldRegistry _world = new();
    private readonly GameDispatcher _dispatcher;
    private readonly CaptureLog _capture;
    private int _connSeq;

    /// <summary>Who is online and where. The source of truth for game state.</summary>
    public WorldRegistry World => _world;

    /// <summary>Experiment: inject a clone spawn (0x082D) after world entry.</summary>
    public bool SpawnTest { get; }

    /// <summary>How long after world entry to inject the experimental spawn.</summary>
    public TimeSpan SpawnDelay { get; }

    /// <summary>
    /// Experiment: once a player starts sending movement, push them a single
    /// character world-state snapshot (a second avatar offset from the player) over
    /// UDP, to see whether the Object.lto spawn walker fires. See
    /// <see cref="CharacterSnapshot"/> and <c>knowledge-base/client/World Object Spawn.md</c>.
    /// </summary>
    public bool SnapshotTest { get; }

    /// <summary>Registers a newly-logged-in player and links it to the session.</summary>
    public Player RegisterPlayer(ClientSession session, string name)
    {
        var player = new Player(_world.AllocateId(), session)
        {
            Name = name,
            World = session.World,
        };
        session.Player = player;
        _world.Add(player);
        return player;
    }

    public GameHost(string bindAddress, int firstPort, int lastPort, string? capturePath = null,
                    bool spawnTest = false, double spawnDelaySeconds = 6, bool snapshotTest = false)
    {
        _address = IPAddress.Parse(bindAddress);
        _firstPort = firstPort;
        _lastPort = lastPort;
        _capture = new CaptureLog(capturePath);
        SpawnTest = spawnTest;
        SpawnDelay = TimeSpan.FromSeconds(spawnDelaySeconds);
        SnapshotTest = snapshotTest;
        _dispatcher = new GameDispatcher(this);
    }

    /// <summary>Id offset for the experiment's second (snapshot) character, so it can't collide with the player.</summary>
    private const uint SnapshotCharacterIdOffset = 4243;

    /// <summary>Known-good test appearance code captured live (male, race nibble 1).</summary>
    private const uint TestAppearanceCode = 0x71088820;

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
            if (session.Player is { } player)
            {
                _world.Remove(player.Id);
            }
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

            // Copy to a byte[] (not a Span) so it can survive the echo's await.
            byte[] datagram = buffer[..result.ReceivedBytes];
            if (previous is not null && datagram.AsSpan().SequenceEqual(previous))
            {
                continue;
            }
            previous = datagram;

            ushort opcode = datagram.Length >= 2 ? (ushort)((datagram[0] << 8) | datagram[1]) : (ushort)0;
            MovementUpdate? move = MovementUpdate.TryParse(datagram);
            if (move is { } update)
            {
                _world.UpdatePosition(update.Session, update);

                // Spawn experiment: echo the player's movement back as the clone
                // entity (spawned via 0x082D with id = player id + 4242), offset in
                // X so it stands beside the player and mirrors their motion. Sent
                // from this port's socket to the client's own UDP endpoint.
                if (SpawnTest)
                {
                    byte[] echo = (byte[])datagram.Clone();
                    BinaryPrimitives.WriteUInt32BigEndian(echo.AsSpan(4), update.Session + 4242);
                    ushort x = BinaryPrimitives.ReadUInt16BigEndian(echo.AsSpan(8));
                    BinaryPrimitives.WriteUInt16BigEndian(echo.AsSpan(8), (ushort)(x + 0x100));
                    try
                    {
                        await udp.SendToAsync(echo, SocketFlags.None, result.RemoteEndPoint, ct);
                    }
                    catch (SocketException)
                    {
                        // best-effort echo
                    }
                }

                // Snapshot experiment: once we know the player's UDP endpoint and a
                // valid position, push them a single world-state snapshot describing a
                // second character standing beside them, and see whether the Object.lto
                // spawn walker fires (i.e. a remote avatar appears). One-shot per player.
                if (SnapshotTest)
                {
                    await MaybeSendSnapshotAsync(udp, result.RemoteEndPoint, update, ct);
                }
            }
            _capture.Udp(port, opcode, datagram, handled: move is not null);
            string suffix = move is { } m
                ? $"  MOVE sess={m.Session} X={m.X} Y={m.Y} Z={m.Z} heading={m.Heading} (~{m.HeadingDegrees:F0}deg)"
                : string.Empty;
            PacketLog.Line($"UDP {WorldPort.FromPort(port)}:{port} <- {result.RemoteEndPoint} len={result.ReceivedBytes}{suffix}");
        }
    }

    // Snapshot experiment (see SnapshotTest): push one character world-state snapshot
    // to a player, exactly once. Looks the player up by movement session id and places
    // the test character beside them using their just-received position, so if the
    // spawn walker fires a second avatar appears next to the player rather than inside.
    private async Task MaybeSendSnapshotAsync(Socket udp, EndPoint client, MovementUpdate at, CancellationToken ct)
    {
        if (!_world.TryGet(at.Session, out Player? player) || player is null || player.SnapshotSent)
        {
            return;
        }
        player.SnapshotSent = true;

        uint entityId = player.Id + SnapshotCharacterIdOffset;
        ushort x = (ushort)(at.X + 0x100); // offset in X so it stands beside the player
        var entry = new SnapshotEntry(entityId, x, at.Y, at.Z, TestAppearanceCode);
        byte[] datagram = CharacterSnapshot.Build(entry);

        PacketLog.Line($"  [snapshot-test] pushing character snapshot id={entityId} " +
            $"at x={x} y={at.Y} z={at.Z} appearance=0x{TestAppearanceCode:X8} ({datagram.Length} bytes)");
        try
        {
            await udp.SendToAsync(datagram, SocketFlags.None, client, ct);
        }
        catch (SocketException e)
        {
            PacketLog.Line($"  [snapshot-test] send failed: {e.Message}");
        }
    }
}
