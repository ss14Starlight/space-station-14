using Content.Server.Atmos.EntitySystems;
using Content.Server._Starlight.Plasmaman.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Plasmaman.EntitySystems;

public sealed class PlasmamanIgnitionSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> PlasmamanSafeTag = "PlasmamanSafe";

    private const float UpdateTimer = 1f;
    private float _timer;

    public override void Update(float frameTime)
    {
        _timer += frameTime;

        if (_timer < UpdateTimer)
            return;

        _timer -= UpdateTimer;

        var enumerator = EntityQueryEnumerator<PlasmamanIgnitionComponent, MobStateComponent, FlammableComponent, InventoryComponent>();
        while (enumerator.MoveNext(out var uid, out var ignition, out var mobState, out var flammable, out var inventory))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            var environment = _atmosphere.GetContainingMixture(uid, excite: true);
            if (environment == null || environment[(int) ignition.Gas] < ignition.MolesToIgnite)
                continue;

            var fireStacks = GetExposureFireStacks((uid, inventory), ignition);
            if (fireStacks <= 0f)
                continue;

            _flammable.AdjustFireStacks(uid, fireStacks, flammable, true);
        }
    }

    private float GetExposureFireStacks(Entity<InventoryComponent> ent, PlasmamanIgnitionComponent ignition)
    {
        var exposed = false;
        var fireStacks = ignition.BaseFireStacks;

        if (!IsProtected(ent, "jumpsuit"))
        {
            exposed = true;
            fireStacks += ignition.JumpsuitFireStacks;
        }

        if (!IsProtected(ent, "head"))
        {
            exposed = true;
            fireStacks += ignition.HeadFireStacks;
        }

        if (!IsProtected(ent, "gloves"))
        {
            exposed = true;
            fireStacks += ignition.GlovesFireStacks;
        }

        return exposed ? fireStacks : 0f;
    }

    private bool IsProtected(Entity<InventoryComponent> ent, string slot)
    {
        return _inventory.TryGetSlotEntity(ent.Owner, slot, out var item, ent.Comp) &&
               item != null &&
               _tag.HasTag(item.Value, PlasmamanSafeTag);
    }
}
