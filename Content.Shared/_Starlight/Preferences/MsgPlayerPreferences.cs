using System.IO;
using Content.Shared._Starlight.Player;
using Content.Shared.Preferences;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Preferences;

/// Intended to get preference data from another player other than the client requesting it.
/// <remarks>Virtual so that other things can inherit for different purposes.</remarks>
[Virtual]
public class MsgPlayerPreferences : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public PlayerPreferences Preferences = null!;
    public MinimalPlayerInfo PlayerInfo = null!;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var length = buffer.ReadVariableInt32();
        using (var stream = new MemoryStream())
        {
            buffer.ReadAlignedMemory(stream, length);
            serializer.DeserializeDirect(stream, out Preferences);
        }

        length = buffer.ReadVariableInt32();
        using (var stream = new MemoryStream())
        {
            buffer.ReadAlignedMemory(stream, length);
            serializer.DeserializeDirect(stream, out PlayerInfo);
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        using (var stream = new MemoryStream())
        {
            serializer.SerializeDirect(stream, Preferences);
            buffer.WriteVariableInt32((int)stream.Length);
            stream.TryGetBuffer(out var segment);
            buffer.Write(segment);
        }

        using (var stream = new MemoryStream())
        {
            serializer.SerializeDirect(stream, PlayerInfo);
            buffer.WriteVariableInt32((int)stream.Length);
            stream.TryGetBuffer(out var segment);
            buffer.Write(segment);
        }
    }
}
