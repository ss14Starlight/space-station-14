using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Preferences;

public sealed class MsgPreferenceConfirmationResult : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public bool Accepted;
    public string? RejectionMessage;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Accepted = buffer.ReadBoolean();
        buffer.ReadPadBits();

        var length = buffer.ReadVariableInt32();
        using var stream = new MemoryStream();
        buffer.ReadAlignedMemory(stream, length);
        serializer.DeserializeDirect(stream, out RejectionMessage);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Accepted);
        buffer.WritePadBits();

        using var stream = new MemoryStream();
        serializer.SerializeDirect(stream, RejectionMessage);
        buffer.WriteVariableInt32((int)stream.Length);
        stream.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}
