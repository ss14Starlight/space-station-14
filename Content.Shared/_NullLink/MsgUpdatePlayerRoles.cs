using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._NullLink;
public sealed class MsgUpdatePlayerRoles : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public HashSet<ulong> Roles = [];
    public string? DiscordLink;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        DiscordLink = buffer.ReadString();

        var length = buffer.ReadVariableInt32();
        Roles.Clear();
        for (var i = 0; i < length; i++)
            Roles.Add(buffer.ReadUInt64());
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(DiscordLink);

        buffer.WriteVariableInt32(Roles.Count);
        foreach (var role in Roles)
            buffer.Write(role);
    }

    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableOrdered;
}