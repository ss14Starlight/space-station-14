using Content.Server._Starlight.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared._Starlight.Traits.Antags;

namespace Content.Server._Starlight.Objectives.Systems;

public sealed class UngloriousRequirementSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UngloriousRequirementComponent, RequirementCheckEvent>(OnCheck);
    }

    private void OnCheck(Entity<UngloriousRequirementComponent> ent, ref RequirementCheckEvent args)
    {
        if (args.Mind.OwnedEntity is { } body && HasComp<UngloriousComponent>(body))
            args.Cancelled = true;
    }
}
