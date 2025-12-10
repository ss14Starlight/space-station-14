using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Dice.DestinyDice;

[Serializable, NetSerializable]
public sealed class DestinyDiceEffectExecutionEvent(NetEntity uid, NetEntity roller, NetEntity? grid, IDestinyDiceEffect effect) : EntityEventArgs
{
    public readonly NetEntity Uid = uid;
    public readonly NetEntity? Grid = grid;
    public readonly NetEntity Roller = roller;
    public readonly IDestinyDiceEffect Effect = effect;
}