using Content.Shared.Movement.Systems;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.RedundantMovement;

public sealed class RedundantMovementAckMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Entity;

    public GameTick Tick { get; set; }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Tick);
    }

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Tick = buffer.ReadGameTick();
    }
}

public sealed class RedundantMovementMessage : NetMessage
{
    /// <summary>The tick that the message was sent on</summary>
    public GameTick SentTick { get; set; }

    /// <summary>The input data for a number of ticks, with the last being <see cref="SentTick"/>.</summary>
    public List<TickInputData> TickData { get; set; } = [];

    public override MsgGroups MsgGroup => MsgGroups.Entity; // => unrelialble transport

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        SentTick = buffer.ReadGameTick();
        int count = buffer.ReadByte();
        TickData.EnsureCapacity(count);
        for (int i = 0; i < count; i++)
        {
            var tick = buffer.ReadGameTick();
            var finalState = (MoveButtons)buffer.ReadByte();
            var changes = new InputChange[buffer.ReadByte()];
            for (int j = 0; j < changes.Length; j++)
            {
                changes[j] = new(buffer.ReadUInt16(), (MoveButtons)buffer.ReadByte());
            }

            TickData.Add(new(tick, finalState, changes));
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(SentTick);

        // count is a byte, so only send up to 255
        int numTicks = int.Clamp(TickData.Count, 0, byte.MaxValue);
        buffer.Write((byte)numTicks);

        // write the last (most recent) if overflow, though this should never happen
        // because we should be limiting how many redundant we're sending through here to like, 5 or something
        for (int i = TickData.Count - numTicks; i < TickData.Count; i++)
        {
            var data = TickData[i];
            buffer.Write(data.Tick);
            buffer.Write((byte)data.FinalInput);
            int numChanges = int.Min(data.Changes.Length, byte.MaxValue);
            buffer.Write((byte)numChanges);
            for (int j = data.Changes.Length - numChanges; j < data.Changes.Length; j++)
            {
                var change = data.Changes[j];
                buffer.Write(change.Subtick);
                buffer.Write((byte)change.HeldButtons);
            }
        }
    }

    public override string ToString()
    {
        return $"RMove: tick {SentTick} sending {TickData.Count} ticks of redundancy";
    }
}

public record struct InputChange(ushort Subtick, MoveButtons HeldButtons);

public record struct TickInputData(GameTick Tick, MoveButtons FinalInput, InputChange[] Changes);
