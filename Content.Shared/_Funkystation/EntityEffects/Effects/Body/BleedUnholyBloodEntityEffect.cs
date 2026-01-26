using Robust.Shared.Prototypes;
using Content.Shared.EntityEffects;

namespace Content.Shared._Funkystation.EntityEffects.Effects.Body;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class BleedUnholyBlood : EntityEffectBase<BleedUnholyBlood>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-bleed-unholy-blood", ("chance", Probability));
}
