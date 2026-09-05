using Content.Shared._Starlight.DestinyDice.Effects;
using Content.Shared.EntityEffects;

namespace Content.Shared._Starlight.DestinyDice.EffectSystems;

/// Acts as a proxy to apply effects to all effect targets.
public sealed partial class DestinyDiceProxyEffectSystem : EntityEffectSystem<DestinyDiceComponent, DestinyDiceProxyEffect>
{
    [Dependency] private DestinyDiceSystem _dd = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    protected override void Effect(Entity<DestinyDiceComponent> entity,
        ref EntityEffectEvent<DestinyDiceProxyEffect> args)
    {
        if (!_dd.GetEffectTargets(entity, out var targets)) return;
        foreach (var target in targets)
            _effects.ApplyEffects(target, args.Effect.ProxiedEffects);
    }
}
