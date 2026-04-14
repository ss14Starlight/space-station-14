using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Starlight;

public sealed class MsgUpdatePlayerStatus : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public PlayerData? Player;
    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        if (buffer.ReadBoolean())
        {
            buffer.ReadPadBits();

            Dictionary<string, double> resources = [];

            var resourcesCount = buffer.ReadInt32();
            resources.EnsureCapacity(resourcesCount);
            for (int i = 0; i < resourcesCount; i++)
            {
                var key = buffer.ReadString();
                var value = buffer.ReadDouble();

                resources[key] = value;
            }
            var title = buffer.ReadString();
            var ghostTheme = buffer.ReadString();

            Player = new PlayerData
            {
                Title = title,
                GhostTheme = ghostTheme,
                Resources = resources,
            };
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Player != null);

        if (Player == null) return;

        buffer.WritePadBits();

        var resources = Player.Resources ?? [];
        buffer.Write(resources.Count);

        foreach (var (key, value) in resources)
        {
            buffer.Write(key);
            buffer.Write(value);
        }

        buffer.Write(Player.Title);
        buffer.Write(Player.GhostTheme);
    }

    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableOrdered;
}
