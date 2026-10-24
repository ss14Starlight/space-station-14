using Content.Shared._Starlight.DestinyDice.Effects;
using Content.Shared.EntityEffects;

namespace Content.Shared._Starlight.DestinyDice.EffectSystems;

/// <summary>
/// Acts as a proxy to apply effects to all effect targets.
/// </summary>
public sealed partial class DestinyDiceProxyEffectSystem : EntityEffectSystem<DestinyDiceComponent, DestinyDiceProxyEffect>
{
    [Dependency] private DestinyDiceSystem _dd = null!;
    [Dependency] private SharedEntityEffectsSystem _effects = null!;

    protected override void Effect(Entity<DestinyDiceComponent> entity,
        ref EntityEffectEvent<DestinyDiceProxyEffect> args)
    {
        if (!_dd.GetEffectTargets(entity, out var targets)) return;
        foreach (var target in targets)
            _effects.ApplyEffects(target, args.Effect.ProxiedEffects);
    }
}
