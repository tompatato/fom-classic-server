namespace FOM.Server;

/// <summary>
/// Console logging of traffic and lifecycle events, mirroring the reference stub's
/// output. Thread-safe. Set <c>FOM_QUIET=1</c> to suppress per-packet hexdumps.
/// </summary>
public static class PacketLog
{
    private static readonly Lock Gate = new();
    private static readonly bool Quiet = Environment.GetEnvironmentVariable("FOM_QUIET") == "1";

    public static void Packet(string direction, ClientSession peer, ushort opcode, ReadOnlySpan<byte> body)
    {
        string name = OpcodeNames.Get(opcode);
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
