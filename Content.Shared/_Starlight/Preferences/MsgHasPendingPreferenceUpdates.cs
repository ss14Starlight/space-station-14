using System.IO;
using Content.Shared._Starlight.Player;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Preferences;

/// <summary>
/// Notify the client that there are pending edits to their preferences so that they can view, accept, or reject them.
/// <see cref="MsgPlayerPreferences.Preferences"/> holds the player's current preferences with the pending edits applied.
/// </summary>
public sealed class MsgHasPendingPreferenceUpdates : MsgPlayerPreferences
{
    /// <summary>
    /// The last admin to modify this preference data.
    /// </summary>
    public MinimalPlayerInfo LastAdmin = null!;

    /// <summary>
    /// Revision of the pending edits, sent back in <see cref="MsgPreferenceConfirmationResult"/>.
    /// </summary>
    public int Revision;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        base.ReadFromBuffer(buffer, serializer);
        var length = buffer.ReadVariableInt32();
        using (var stream = new MemoryStream())
        {
            buffer.ReadAlignedMemory(stream, length);
            serializer.DeserializeDirect(stream, out LastAdmin);
        }

        Revision = buffer.ReadVariableInt32();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        base.WriteToBuffer(buffer, serializer);
        using (var stream = new MemoryStream())
        {
            serializer.SerializeDirect(stream, LastAdmin);
            buffer.WriteVariableInt32((int)stream.Length);
            stream.TryGetBuffer(out var segment);
            buffer.Write(segment);
        }

        buffer.WriteVariableInt32(Revision);
    }
}
