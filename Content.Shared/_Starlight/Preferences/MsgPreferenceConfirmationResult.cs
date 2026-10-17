using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Preferences;

public sealed class MsgPreferenceConfirmationResult : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public bool Accepted;

    /// <summary>
    /// The <see cref="MsgHasPendingPreferenceUpdates.Revision"/> being responded to.
    /// </summary>
    public int Revision;
    public string? RejectionMessage;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Accepted = buffer.ReadBoolean();
        buffer.ReadPadBits();
        Revision = buffer.ReadVariableInt32();

        var hasMessage = buffer.ReadBoolean();
        buffer.ReadPadBits();
        RejectionMessage = hasMessage ? buffer.ReadString() : null;
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Accepted);
        buffer.WritePadBits();
        buffer.WriteVariableInt32(Revision);

        buffer.Write(RejectionMessage is not null);
        buffer.WritePadBits();
        if (RejectionMessage is not null)
            buffer.Write(RejectionMessage);
    }
}
