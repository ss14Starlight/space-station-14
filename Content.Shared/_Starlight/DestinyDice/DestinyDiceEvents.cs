using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.DestinyDice;

[Serializable, NetSerializable]
public sealed class DestinyDiceEffectEndEvent : EntityEventArgs;

[Serializable]
public sealed class DestinyDiceExecuteEffectEvent(DestinyDiceEffect effect) : EntityEventArgs
{
    public DestinyDiceEffect Effect = effect;
}
