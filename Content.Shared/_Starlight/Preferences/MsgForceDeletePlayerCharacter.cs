using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Preferences;

public sealed class MsgForceDeletePlayerCharacter : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public NetUserId UserId;
    public int Slot;
    public bool NotifyPlayer;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Slot = buffer.ReadInt32();
        using (var stream = new MemoryStream())
        {
            var length = buffer.ReadVariableInt32();
            buffer.ReadAlignedMemory(stream, length);
            serializer.DeserializeDirect(stream, out UserId);
        }

        NotifyPlayer = buffer.ReadBoolean();
        buffer.ReadPadBits();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Slot);

        using (var stream = new MemoryStream())
        {
            serializer.SerializeDirect(stream, UserId);
            buffer.WriteVariableInt32((int)stream.Length);
            stream.TryGetBuffer(out var segment);
            buffer.Write(segment);
        }

        buffer.Write(NotifyPlayer);
        buffer.WritePadBits();
    }
}
