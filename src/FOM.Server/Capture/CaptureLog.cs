using System.Text.Json;
using System.Text.Json.Serialization;

namespace FOM.Server.Capture;

/// <summary>
/// Writes a machine-readable JSONL capture of a session (one <see cref="CaptureEntry"/>
/// per line) so a run can be analyzed offline by <see cref="CaptureAnalyzer"/>.
/// Thread-safe. A null/empty path yields a no-op sink, so callers need no null checks.
/// </summary>
public sealed class CaptureLog : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly TextWriter? _writer;
    private readonly Lock _gate = new();
    private bool _disposed;

    public CaptureLog(string? path)
    {
        _writer = string.IsNullOrEmpty(path)
            ? null
            : new StreamWriter(path, append: false) { AutoFlush = true };
    }

    public bool Enabled => _writer is not null;

    public void Packet(string direction, ClientSession peer, ushort opcode, ReadOnlySpan<byte> body, bool handled)
    {
        if (_writer is null)
        {
            return;
        }

        Write(new CaptureEntry
        {
            Event = "packet",
            Dir = direction,
            Conn = peer.ConnId,
            Port = peer.Port,
            World = peer.World.ToString(),
            Opcode = $"0x{opcode:X4}",
            Name = OpcodeNames.Get(opcode),
            Len = body.Length,
            Hex = Convert.ToHexString(body),
            Handled = handled,
        });
    }

    public void Event(string type, int? conn = null, int? port = null, string? detail = null)
    {
        if (_writer is null)
        {
            return;
        }

        Write(new CaptureEntry { Event = type, Conn = conn, Port = port, Detail = detail });
    }

    private void Write(CaptureEntry entry)
    {
        entry = entry with { Ts = DateTime.Now.ToString("HH:mm:ss.fff") };
        string line = JsonSerializer.Serialize(entry, JsonOptions);
        lock (_gate)
        {
            if (_disposed)
            {
                return; // best-effort: never throw from logging, even during shutdown
            }
            _writer!.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _writer?.Dispose();
        }
    }
}
