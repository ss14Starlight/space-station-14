using Content.Shared.EntityEffects;

namespace Content.Shared._Starlight.DestinyDice.Effects;

public sealed partial class DestinyDiceProxyEffect : EntityEffectBase<DestinyDiceProxyEffect>
{
    [DataField("effects", required: true)] public EntityEffect[] ProxiedEffects = [];
}
