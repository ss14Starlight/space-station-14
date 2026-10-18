using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight;

public sealed class MsgUpdatePlayerStatus : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public PlayerData? Player;
    public Dictionary<string, double>? Resources;

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

            Resources = resources;
            Player = new PlayerData
            {
                Title = title,
                GhostTheme = ghostTheme,
            };
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Player != null);

        if (Player == null) return;

        buffer.WritePadBits();

        var resources = Resources;
        buffer.Write(resources?.Count ?? 0);

        if (resources != null)
        {
            foreach (var (key, value) in resources)
            {
                buffer.Write(key);
                buffer.Write(value);
            }
        }

        buffer.Write(Player.Title);
        buffer.Write(Player.GhostTheme);
    }

    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableOrdered;
}
