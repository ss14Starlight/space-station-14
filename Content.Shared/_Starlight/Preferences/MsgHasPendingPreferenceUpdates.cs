using System.IO;
using Content.Shared._Starlight.Player;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Preferences;

/// Notify the client that there are pending edits to their preferences so that they can view, accept, or reject them.
public sealed class MsgHasPendingPreferenceUpdates : MsgPlayerPreferences
{
    /// The last admin to modify this preference data.
    public MinimalPlayerInfo? LastAdmin;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        base.ReadFromBuffer(buffer, serializer);
        var length = buffer.ReadVariableInt32();
        using var stream = new MemoryStream();
        buffer.ReadAlignedMemory(stream, length);
        serializer.DeserializeDirect(stream, out LastAdmin);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        base.WriteToBuffer(buffer, serializer);
        var stream = new MemoryStream();
        serializer.SerializeDirect(stream, LastAdmin);
        buffer.WriteVariableInt32((int)stream.Length);
        stream.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}
