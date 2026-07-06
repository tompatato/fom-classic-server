namespace FOM.Protocol;

/// <summary>
/// Accumulates bytes arriving from a TCP stream and yields complete frames as they
/// become available — handling the two realities of TCP: a frame split across
/// several reads, and several frames coalesced into one read. Not thread-safe;
/// use one per connection.
/// </summary>
public sealed class FrameBuffer
{
    private byte[] _buffer;
    private int _length; // number of valid bytes at the front of _buffer

    public FrameBuffer(int initialCapacity = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);
        _buffer = new byte[initialCapacity];
    }

    /// <summary>Bytes buffered but not yet consumed as a complete frame.</summary>
    public int Buffered => _length;

    /// <summary>Appends freshly received bytes.</summary>
    public void Append(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(_length + data.Length);
        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;
    }

    /// <summary>
    /// Pulls the next complete frame from the front of the buffer, if one is fully
    /// present. The returned <paramref name="body"/> is a fresh copy safe to retain.
    /// </summary>
    public bool TryReadFrame(out ushort opcode, out byte[] body)
    {
        if (PacketFrame.TryRead(_buffer.AsSpan(0, _length), out opcode, out ReadOnlySpan<byte> span, out int consumed))
        {
            body = span.ToArray();
            int remaining = _length - consumed;
            if (remaining > 0)
            {
                _buffer.AsSpan(consumed, remaining).CopyTo(_buffer);
            }
            _length = remaining;
            return true;
        }

        body = [];
        return false;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }

        int capacity = _buffer.Length * 2;
        while (capacity < required)
        {
            capacity *= 2;
        }
        Array.Resize(ref _buffer, capacity);
    }
}
