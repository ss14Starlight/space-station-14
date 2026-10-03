using Content.Shared.Movement.Systems;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.RedundantMovement;

public sealed class RedundantMovementAckMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Entity;

    public uint Sequence { get; set; }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer) => buffer.Write(Sequence);

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer) => Sequence = buffer.ReadUInt32();
}

public sealed class RedundantMovementMessage : NetMessage
{
    /// <summary>The tick that the message was sent on</summary>
    public GameTick SentTick { get; set; }

    /// <summary>
    /// Increases with every message. Tick data from a higher sequence replaces data for the same tick from a lower one,
    /// since the client may rewrite ticks it already sent when its predicted tick moves backwards.
    /// </summary>
    public uint Sequence { get; set; }

    /// <summary>The input data for a number of ticks, with the last being <see cref="SentTick"/>.</summary>
    public List<TickInputData> TickData { get; set; } = [];

    public override MsgGroups MsgGroup => MsgGroups.Entity; // => unrelialble transport

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        SentTick = buffer.ReadGameTick();
        Sequence = buffer.ReadUInt32();
        int count = buffer.ReadByte();
        TickData.EnsureCapacity(count);
        for (int i = 0; i < count; i++)
        {
            var tick = buffer.ReadGameTick();
            var finalState = (MoveButtons)buffer.ReadByte();
            var changes = new InputChange[buffer.ReadByte()];
            for (int j = 0; j < changes.Length; j++)
            {
                changes[j] = new(buffer.ReadUInt16(), new(buffer.ReadByte()));
            }

            TickData.Add(new(tick, new(finalState), changes));
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(SentTick);
        buffer.Write(Sequence);

        // count is a byte, so only send up to 255
        int numTicks = int.Clamp(TickData.Count, 0, byte.MaxValue);
        buffer.Write((byte)numTicks);

        // write the last (most recent) if overflow, though this should never happen
        // because we should be limiting how many redundant we're sending through here to like, 5 or something
        for (int i = TickData.Count - numTicks; i < TickData.Count; i++)
        {
            var data = TickData[i];
            buffer.Write(data.Tick);
            buffer.Write(data.FinalInput.RawValue);
            int numChanges = int.Min(data.Changes.Length, byte.MaxValue);
            buffer.Write((byte)numChanges);
            for (int j = data.Changes.Length - numChanges; j < data.Changes.Length; j++)
            {
                var change = data.Changes[j];
                buffer.Write(change.Subtick);
                buffer.Write(change.HeldButtons.RawValue);
            }
        }
    }

    public override string ToString() => $"RMove: tick {SentTick} seq {Sequence} sending {TickData.Count} ticks of redundancy";
}

public record struct InputChange(ushort Subtick, PackedMovementButtons HeldButtons);

public record struct TickInputData(GameTick Tick, PackedMovementButtons FinalInput, InputChange[] Changes);

public record struct PackedMovementButtons(byte RawValue)
{
    public const int ShuttleModeBit = 1 << 7;

    public PackedMovementButtons(MoveButtons move) : this((byte)move) { }

    public PackedMovementButtons(ShuttleButtons shuttle) : this((byte)((int)shuttle | ShuttleModeBit)) { }

    public MoveButtons MoveButtons
    {
        readonly get => (RawValue & ShuttleModeBit) == 0 ? (MoveButtons)RawValue : MoveButtons.None;
        set => RawValue = (byte)value;
    }

    public ShuttleButtons ShuttleButtons
    {
        readonly get => (RawValue & ShuttleModeBit) == ShuttleModeBit ? (ShuttleButtons)(RawValue & ~ShuttleModeBit) : ShuttleButtons.None;
        set => RawValue = (byte)((int)value | ShuttleModeBit);
    }

    public readonly bool IsShuttleInputActive => (RawValue & ShuttleModeBit) != 0;

    public readonly bool HasInput => (RawValue & ~ShuttleModeBit) != 0;
}
