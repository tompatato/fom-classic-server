namespace FOM.Protocol.Messages;

/// <summary>A server→client packet that can write its own body.</summary>
public interface IServerMessage
{
    /// <summary>The opcode this message is framed under.</summary>
    PacketId Id { get; }

    /// <summary>Writes the body (everything after the frame header).</summary>
    void WriteBody(PacketWriter writer);
}

public static class ServerMessageExtensions
{
    /// <summary>Serializes the message to a complete wire frame.</summary>
    public static byte[] ToFrame(this IServerMessage message)
    {
        var writer = new PacketWriter();
        message.WriteBody(writer);
        return writer.ToFrame(message.Id);
    }
}
