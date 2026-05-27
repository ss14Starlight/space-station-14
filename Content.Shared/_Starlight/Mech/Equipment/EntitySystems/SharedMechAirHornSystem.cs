using Content.Shared.Mech;
using Content.Shared._Starlight.Mech.Equipment.Components;

namespace Content.Shared._Starlight.Mech.Equipment.EntitySystems;

public abstract class SharedMechAirHornSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechAirHornComponent, MechActivateAirHornEvent>(OnHonkHorn);
    }

    protected abstract void OnHonkHorn(EntityUid uid, MechAirHornComponent comp, MechActivateAirHornEvent args);
}
