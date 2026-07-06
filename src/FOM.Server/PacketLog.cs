using FOM.Protocol;

namespace FOM.Server;

/// <summary>
/// Console logging of traffic and lifecycle events, mirroring the reference stub's
/// output. Thread-safe. Set <c>FOM_QUIET=1</c> to suppress per-packet hexdumps.
/// </summary>
public static class PacketLog
{
    private static readonly Lock Gate = new();
    private static readonly bool Quiet = Environment.GetEnvironmentVariable("FOM_QUIET") == "1";

    // Tentative opcode labels for readability; "?" where still unmapped.
    private static readonly Dictionary<ushort, string> Names = new()
    {
        [(ushort)PacketId.LOGIN_REQUEST] = "LOGIN_REQUEST",
        [(ushort)PacketId.LOGIN_RETURN] = "LOGIN_RETURN",
        [(ushort)PacketId.LOAD_CHAR] = "LOAD_CHAR",
        [(ushort)PacketId.PING] = "PING",
        [(ushort)PacketId.ENTER_WORLD] = "ENTER_WORLD",
        [(ushort)PacketId.EXIT_APT] = "EXIT_APT",
        [(ushort)PacketId.ZONE_UPDATE] = "ZONE_UPDATE",
        [(ushort)PacketId.CHAT] = "CHAT",
        [(ushort)PacketId.MOVEMENT] = "MOVEMENT",
    };

    public static void Packet(string direction, ClientSession peer, ushort opcode, ReadOnlySpan<byte> body)
    {
        string name = Names.GetValueOrDefault(opcode, "?");
        string header = $"conn#{peer.ConnId} {peer.World}:{peer.Port} {direction} " +
                        $"0x{opcode:X4} {name,-13} len={body.Length}";
        string text = Quiet || body.IsEmpty ? header : header + "\n" + HexDump(body);
        Write(text);
    }

    public static void Line(string text) => Write(text);

    private static void Write(string text)
    {
        string stamped = $"[{DateTime.Now:HH:mm:ss.fff}] {text}";
        lock (Gate)
        {
            Console.WriteLine(stamped);
        }
    }

    private static string HexDump(ReadOnlySpan<byte> data, int width = 16)
    {
        var sb = new System.Text.StringBuilder();
        for (int offset = 0; offset < data.Length; offset += width)
        {
            int end = Math.Min(offset + width, data.Length);
            var hex = new System.Text.StringBuilder();
            var ascii = new System.Text.StringBuilder();
            for (int i = offset; i < end; i++)
            {
                hex.Append($"{data[i]:X2} ");
                ascii.Append(data[i] is >= 32 and < 127 ? (char)data[i] : '.');
            }
            sb.Append($"    {offset:X4}  {hex.ToString().PadRight(width * 3),-48} |{ascii}|");
            if (end < data.Length)
            {
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }
}
