using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._NullLink;

public sealed class MsgUpdatePlayerPlayTime : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public Dictionary<string, Dictionary<string, TimeSpan>> RolePlayTimePerServer = [];

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var serverCount = buffer.ReadVariableInt32();
        RolePlayTimePerServer.EnsureCapacity(serverCount);

        for (var i = 0; i < serverCount; i++)
        {
            var serverName = buffer.ReadString();
            var roleCount = buffer.ReadVariableInt32();
            var roles = new Dictionary<string, TimeSpan>(roleCount);

            for (var j = 0; j < roleCount; j++)
            {
                var roleName = buffer.ReadString();
                var ticks = buffer.ReadInt64();
                roles[roleName] = TimeSpan.FromTicks(ticks);
            }

            RolePlayTimePerServer[serverName] = roles;
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(RolePlayTimePerServer.Count);

        foreach (var (serverName, roles) in RolePlayTimePerServer)
        {
            buffer.Write(serverName);
            buffer.WriteVariableInt32(roles.Count);

            foreach (var (roleName, time) in roles)
            {
                buffer.Write(roleName);
                buffer.Write(time.Ticks);
            }
        }
    }
    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableOrdered;
}
