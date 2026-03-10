using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._NullLink;

public sealed class MsgUpdatePlayerResources : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public Dictionary<string, double> Resources = [];

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var resourcesCount = buffer.ReadVariableInt32();
        Resources.EnsureCapacity(resourcesCount);

        for (var i = 0; i < resourcesCount; i++)
        {
            var resourceName = buffer.ReadString();
            var resourceValue = buffer.ReadDouble();

            Resources[resourceName] = resourceValue;
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Resources.Count);

        foreach (var (resourceName, value) in Resources)
        {
            buffer.Write(resourceName);
            buffer.Write(value);
        }
    }
    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableOrdered;
}
