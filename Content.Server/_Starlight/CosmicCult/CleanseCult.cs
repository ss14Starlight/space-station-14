using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.CosmicCult;

public sealed partial class CleanseCult : EntityEffectBase<CleanseCult>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-cleanse-cultist", ("chance", Probability));
    }

    public override void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        if (entityManager.HasComponent<CosmicCultComponent>(target))
            entityManager.EnsureComponent<CleanseCultComponent>(target); // We just slap them with the component and let the Deconversion system handle the rest.
    }
}
