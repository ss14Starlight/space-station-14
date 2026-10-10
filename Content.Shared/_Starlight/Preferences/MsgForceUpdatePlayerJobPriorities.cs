using System.IO;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Preferences;

public sealed class MsgForceUpdatePlayerJobPriorities : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public Dictionary<ProtoId<JobPrototype>, JobPriority> JobPriorities = null!;
    public NetUserId UserId;
    public bool NotifyPlayer;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        using (var stream = new MemoryStream())
        {
            var length = buffer.ReadVariableInt32();
            buffer.ReadAlignedMemory(stream, length);
            serializer.DeserializeDirect(stream, out JobPriorities);
        }

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
        using (var stream = new MemoryStream())
        {
            serializer.SerializeDirect(stream, JobPriorities);
            buffer.WriteVariableInt32((int)stream.Length);
            stream.TryGetBuffer(out var segment);
            buffer.Write(segment);
        }

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
