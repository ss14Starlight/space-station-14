using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight;

public sealed class MsgUpdatePlayerStatus : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public PlayerData? Player;
    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        if (buffer.ReadBoolean())
        {
            buffer.ReadPadBits();

            var title = buffer.ReadString();
            var ghostTheme = buffer.ReadString();

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

        if (Player == null)
            return;

        buffer.WritePadBits();
        buffer.Write(Player.Title);
        buffer.Write(Player.GhostTheme);
    }

    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableOrdered;
}
