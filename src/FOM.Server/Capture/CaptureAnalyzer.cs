using System.Text;
using System.Text.Json;

namespace FOM.Server.Capture;

/// <summary>Summary of a capture: what flowed, and what the server didn't handle.</summary>
public sealed record CaptureReport
{
    public int TotalPackets { get; init; }
    public int Connects { get; init; }
    public int Disconnects { get; init; }

    /// <summary>Per-(direction, opcode) counts, e.g. "C->S 0x07D1 LOGIN_REQUEST" → 1.</summary>
    public required IReadOnlyList<OpcodeCount> Traffic { get; init; }

    /// <summary>Distinct opcodes the server logged as unhandled, most frequent first.</summary>
    public required IReadOnlyList<OpcodeCount> Unmapped { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public string Render()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"packets={TotalPackets}  connects={Connects}  disconnects={Disconnects}  errors={Errors.Count}");
        sb.AppendLine("traffic:");
        foreach (OpcodeCount t in Traffic)
        {
            sb.AppendLine($"  {t.Key,-28} x{t.Count}");
        }
        if (Unmapped.Count > 0)
        {
            sb.AppendLine("UNMAPPED (no handler):");
            foreach (OpcodeCount u in Unmapped)
            {
                sb.AppendLine($"  {u.Key,-28} x{u.Count}");
            }
        }
        foreach (string e in Errors)
        {
            sb.AppendLine($"ERROR: {e}");
        }
        return sb.ToString().TrimEnd();
    }
}

public sealed record OpcodeCount(string Key, int Count);

/// <summary>Reduces a JSONL capture to a <see cref="CaptureReport"/>.</summary>
public static class CaptureAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public static CaptureReport AnalyzeFile(string path) => Analyze(File.ReadLines(path));

    public static CaptureReport Analyze(IEnumerable<string> jsonlLines)
    {
        var traffic = new Dictionary<string, int>();
        var unmapped = new Dictionary<string, int>();
        var errors = new List<string>();
        int packets = 0, connects = 0, disconnects = 0;

        foreach (string line in jsonlLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            CaptureEntry? entry = JsonSerializer.Deserialize<CaptureEntry>(line, JsonOptions);
            if (entry is null)
            {
                continue;
            }

            switch (entry.Event)
            {
                case "connect":
                    connects++;
                    break;
                case "disconnect":
                    disconnects++;
                    break;
                case "error":
                    errors.Add(entry.Detail ?? "(no detail)");
                    break;
                case "packet":
                    packets++;
                    string key = $"{entry.Dir} {entry.Opcode} {entry.Name}".Trim();
                    traffic[key] = traffic.GetValueOrDefault(key) + 1;
                    if (entry.Handled == false)
                    {
                        string opKey = $"{entry.Opcode} {entry.Name}".Trim();
                        unmapped[opKey] = unmapped.GetValueOrDefault(opKey) + 1;
                    }
                    break;
            }
        }

        return new CaptureReport
        {
            TotalPackets = packets,
            Connects = connects,
            Disconnects = disconnects,
            Traffic = Sort(traffic),
            Unmapped = Sort(unmapped),
            Errors = errors,
        };
    }

    private static List<OpcodeCount> Sort(Dictionary<string, int> counts) =>
        counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
              .Select(kv => new OpcodeCount(kv.Key, kv.Value)).ToList();
}
