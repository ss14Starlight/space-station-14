using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Achievement;

public sealed class MsgAchievementList : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public HashSet<string> UnlockedAchievements = [];
    public Dictionary<string, double> Progress = [];

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var unlockedCount = buffer.ReadVariableInt32();
        UnlockedAchievements = new HashSet<string>(unlockedCount);
        for (var i = 0; i < unlockedCount; i++)
            UnlockedAchievements.Add(buffer.ReadString());

        var progressCount = buffer.ReadVariableInt32();
        Progress = new Dictionary<string, double>(progressCount);
        for (var i = 0; i < progressCount; i++)
        {
            var key = buffer.ReadString();
            var value = buffer.ReadDouble();
            Progress[key] = value;
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(UnlockedAchievements.Count);
        foreach (var id in UnlockedAchievements)
            buffer.Write(id);

        buffer.WriteVariableInt32(Progress.Count);
        foreach (var (key, value) in Progress)
        {
            buffer.Write(key);
            buffer.Write(value);
        }
    }

    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableOrdered;
}
