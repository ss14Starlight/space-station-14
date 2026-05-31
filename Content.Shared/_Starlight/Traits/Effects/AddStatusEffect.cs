using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Traits.Effects;

/// <summary>
/// Adds permanent status effects to the entity.
/// </summary>
public sealed partial class AddStatusEffect : BaseTraitEffect
{
    [DataField(required: true)]
    public HashSet<EntProtoId> StatusEffects { get; private set; } = new();

    public override void Apply(TraitEffectContext ctx)
    {
        if (!ctx.EntMan.EntitySysManager.TryGetEntitySystem(out StatusEffectsSystem? effectsSystem))
            return;

        foreach (var effect in StatusEffects)
            effectsSystem.TrySetStatusEffectDuration(ctx.Player, effect);
    }
}
