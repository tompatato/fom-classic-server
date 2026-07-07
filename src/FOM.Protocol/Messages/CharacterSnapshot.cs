using System.Buffers.Binary;

namespace FOM.Protocol.Messages;

/// <summary>
/// One character in a world-state snapshot, as the Object.lto spawn walker
/// <c>FUN_10036fc0</c> reads it — a fixed <b>32-byte</b> entry. Field offsets and
/// meanings are from <c>knowledge-base/client/World Object Spawn.md</c>.
/// </summary>
public readonly record struct SnapshotEntry(
    uint EntityId,
    ushort X,
    ushort Y,
    ushort Z,
    uint Appearance)
{
    /// <summary>
    /// Type nibble occupying bits 24–27 of the id word. The walker treats
    /// <c>entryType &lt; 0xB</c> as a character (spawns a <c>CCharacter</c>); we use 0.
    /// </summary>
    public byte Type { get; init; }

    /// <summary>
    /// Entry flags word (offset <c>0x0C</c>): <c>&amp;0x1FF</c> = node index (adds
    /// height), <c>&amp;0x10000000</c> = skip-spawn (must be clear to spawn),
    /// <c>&amp;0xF000000</c> = variant. 0 = node 0, spawn, no variant.
    /// </summary>
    public uint Flags { get; init; }
}

/// <summary>
/// Builds a <b>world-state snapshot</b> datagram — the buffer the engine hands to
/// the Object.lto spawn walker <c>FUN_10036fc0</c> (IServerShell method 19), which
/// walks it and instantiates any character not already live. This is the one path
/// that spawns <b>remote player avatars</b>; there is no per-type opcode for them
/// (unlike the <see cref="SpawnMeetingPoint"/> world object). See
/// <c>knowledge-base/client/World Object Spawn.md</c>.
///
/// <para><b>UNCONFIRMED wire format — this is the experiment.</b> Static RE pins the
/// buffer the walker reads: a <c>u16 count</c> at <c>+0x14</c> and an array of
/// 32-byte entries at <c>+0x18</c>, read <b>raw</b> (the walker does <i>not</i>
/// deserialize, so fields are native little-endian x86 struct memory). What is
/// <i>not</i> confirmed is the transport that delivers it: method 19 has no TCP
/// forwarder, so it is almost certainly the <b>UDP world-state channel</b> (movement
/// <c>0x03F3</c> is UDP and ignored by the reliable router). The header before
/// <c>+0x14</c> and the routing opcode are guesses until the live gdb hook confirms
/// them. Fallbacks if the walker doesn't fire: try big-endian fields, or a
/// different <see cref="CandidateOpcode"/>.</para>
/// </summary>
public static class CharacterSnapshot
{
    /// <summary>Offset of the <c>u16</c> entry count within the datagram.</summary>
    public const int CountOffset = 0x14;

    /// <summary>Offset of the first 32-byte entry within the datagram.</summary>
    public const int EntriesOffset = 0x18;

    /// <summary>Bytes per entry (stride).</summary>
    public const int EntryStride = 0x20;

    /// <summary>The walker clamps to at most 50 entries.</summary>
    public const int MaxEntries = 50;

    /// <summary>
    /// Candidate UDP opcode for the down-channel snapshot, written big-endian at the
    /// datagram head to mirror movement's <c>[opcode][len]</c> framing. A guess:
    /// <c>0x03F3</c> is the confirmed UDP world-state (movement) opcode, so the
    /// server→client snapshot plausibly shares the channel. The walker reads
    /// <see cref="CountOffset"/> regardless of this value; it only affects engine
    /// routing.
    /// </summary>
    public const ushort CandidateOpcode = 0x03F3;

    /// <summary>Builds a snapshot datagram carrying <paramref name="entries"/>.</summary>
    public static byte[] Build(IReadOnlyList<SnapshotEntry> entries, ushort opcode = CandidateOpcode)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count > MaxEntries)
        {
            throw new ArgumentOutOfRangeException(nameof(entries),
                $"snapshot holds at most {MaxEntries} entries, got {entries.Count}");
        }

        byte[] datagram = new byte[EntriesOffset + entries.Count * EntryStride];

        // Transport header (bytes 0x00..0x13). Big-endian [opcode][length] like the
        // movement datagram; the remaining reserved bytes stay zero. Layout here is
        // unconfirmed — only the count/entries offsets below are RE-derived.
        BinaryPrimitives.WriteUInt16BigEndian(datagram.AsSpan(0), opcode);
        BinaryPrimitives.WriteUInt16BigEndian(datagram.AsSpan(2), (ushort)(datagram.Length - PacketFrame.HeaderSize));

        // count @ +0x14, little-endian (read raw as a native struct field).
        BinaryPrimitives.WriteUInt16LittleEndian(datagram.AsSpan(CountOffset), (ushort)entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            WriteEntry(datagram.AsSpan(EntriesOffset + i * EntryStride, EntryStride), entries[i]);
        }
        return datagram;
    }

    /// <summary>Builds a single-character snapshot (the common experiment case).</summary>
    public static byte[] Build(SnapshotEntry entry, ushort opcode = CandidateOpcode) =>
        Build([entry], opcode);

    // A 32-byte entry, fields little-endian (walker reads the buffer as raw struct
    // memory — no byte-swap). Offsets 0x10/0x14/0x18 are unused for a spawn (they
    // carry float x/y/z only when *updating* a live non-character object).
    private static void WriteEntry(Span<byte> entry, SnapshotEntry e)
    {
        entry.Clear();
        // +0x00: bits 0–23 = entity id, bits 24–27 = type nibble (< 0xB => character).
        BinaryPrimitives.WriteUInt32LittleEndian(entry, ((uint)e.Type << 24) | (e.EntityId & 0x00FFFFFF));
        // +0x04: low u16 = X, high u16 = Z.
        BinaryPrimitives.WriteUInt32LittleEndian(entry[0x04..], e.X | ((uint)e.Z << 16));
        // +0x08: low u16 = Y.
        BinaryPrimitives.WriteUInt32LittleEndian(entry[0x08..], e.Y);
        // +0x0C: flags (node index / skip-spawn / variant).
        BinaryPrimitives.WriteUInt32LittleEndian(entry[0x0C..], e.Flags);
        // +0x1C: packed appearance code -> SetAppearance.
        BinaryPrimitives.WriteUInt32LittleEndian(entry[0x1C..], e.Appearance);
    }
}
