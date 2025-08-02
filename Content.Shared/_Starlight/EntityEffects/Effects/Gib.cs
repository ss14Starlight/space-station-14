using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityEffects.Effects;

/// <summary>
/// RED MIST!!!
/// </summary>
public sealed partial class Gib : EntityEffect
{
    public override void Effect(EntityEffectBaseArgs args)
    {
        args.EntityManager.System<SharedBodySystem>()
            .GibBody(args.TargetEntity);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-gib");
    }
}
