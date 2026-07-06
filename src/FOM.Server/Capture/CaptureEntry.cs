using System.Text.Json.Serialization;

namespace FOM.Server.Capture;

/// <summary>
/// One line in a JSONL capture. Machine-readable record of a packet or a
/// connection-lifecycle event, so a session can be replayed/analyzed offline.
/// Null fields are omitted on write.
/// </summary>
public sealed record CaptureEntry
{
    [JsonPropertyName("ts")] public string Ts { get; init; } = string.Empty;

    /// <summary>One of: <c>listen</c>, <c>connect</c>, <c>disconnect</c>, <c>packet</c>, <c>error</c>.</summary>
    [JsonPropertyName("event")] public string Event { get; init; } = string.Empty;

    [JsonPropertyName("dir")] public string? Dir { get; init; }        // "C->S" / "S->C"
    [JsonPropertyName("conn")] public int? Conn { get; init; }
    [JsonPropertyName("port")] public int? Port { get; init; }
    [JsonPropertyName("world")] public string? World { get; init; }
    [JsonPropertyName("opcode")] public string? Opcode { get; init; }  // "0x07D1"
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("len")] public int? Len { get; init; }
    [JsonPropertyName("hex")] public string? Hex { get; init; }
    [JsonPropertyName("handled")] public bool? Handled { get; init; }
    [JsonPropertyName("detail")] public string? Detail { get; init; }
}
