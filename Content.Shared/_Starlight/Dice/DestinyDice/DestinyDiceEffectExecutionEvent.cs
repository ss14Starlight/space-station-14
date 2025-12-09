using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Dice.DestinyDice;

[Serializable, NetSerializable]
public sealed class DestinyDiceEffectExecutionEvent(NetEntity uid, IDestinyDiceEffect effect) : EntityEventArgs
{
    public readonly NetEntity Uid = uid;
    public readonly IDestinyDiceEffect Effect = effect;
}