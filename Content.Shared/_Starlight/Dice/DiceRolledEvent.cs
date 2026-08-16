using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Dice;

[Serializable, NetSerializable]
public sealed class DiceRolledEvent(int value) : EntityEventArgs
{
    public readonly int Value = value;
}
