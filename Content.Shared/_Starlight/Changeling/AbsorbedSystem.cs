using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Atmos.Rotting; // starlight edit

namespace Content.Shared._Starlight.Changeling;

public sealed partial class AbsorbedSystem : EntitySystem
{
    [Dependency] private SharedRottingSystem _rotting = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbsorbedComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<AbsorbedComponent, MobStateChangedEvent>(OnMobStateChange);
    }

    private void OnExamine(Entity<AbsorbedComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("changeling-absorb-onexamine"));
        args.PushMarkup(Loc.GetString("changeling-absorb-onexamine-fluid"));
    }

    private void OnMobStateChange(Entity<AbsorbedComponent> ent, ref MobStateChangedEvent args)
    {
        // in case one somehow manages to dehusk someone
        if (args.NewMobState != MobState.Dead)
            RemComp<AbsorbedComponent>(ent);
            // starlight edit - start: The changeling devour changes the rot timer, so we need to reset it when reviving someone
            if (TryComp<PerishableComponent>(ent, out var perishable))
            {
                _rotting.SetRotAfter(ent, TimeSpan.FromMinutes(10), perishable);
            }
            // starlight edit - end
    }
}
