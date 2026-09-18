using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Preferences;

public sealed class MsgForcePlayerCharacterEnable : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public int CharacterIndex;
    public bool EnabledValue;
    public NetUserId UserId;
    public bool NotifyPlayer;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        CharacterIndex = buffer.ReadVariableInt32();
        EnabledValue = buffer.ReadBoolean();

        using (var stream = new MemoryStream())
        {
            var length = buffer.ReadVariableInt32();
            buffer.ReadAlignedMemory(stream, length);
            serializer.DeserializeDirect(stream, out UserId);
        }

        using (var stream = new MemoryStream())
        {
            var length = buffer.ReadVariableInt32();
            buffer.ReadAlignedMemory(stream, length);
            serializer.DeserializeDirect(stream, out NotifyPlayer);
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(CharacterIndex);
        buffer.Write(EnabledValue);

        using (var stream = new MemoryStream())
        {
            serializer.SerializeDirect(stream, UserId);
            buffer.WriteVariableInt32((int)stream.Length);
            stream.TryGetBuffer(out var segment);
            buffer.Write(segment);
        }

        using (var stream = new MemoryStream())
        {
            serializer.SerializeDirect(stream, NotifyPlayer);
            buffer.WriteVariableInt32((int)stream.Length);
            stream.TryGetBuffer(out var segment);
            buffer.Write(segment);
        }
    }
}
