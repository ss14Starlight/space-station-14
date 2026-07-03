using Content.Server._Starlight.Clothing.Components;
using Content.Server.Electrocution;
using Content.Server.Lightning;
using Content.Shared.Emp;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Clothing.Systems;

/// <summary>
/// Handles the bluespace power cell's overcharge mechanic: when inserted into anything that is
/// neither capacitor gloves nor a charger, it drains all power from the host device, fires
/// lightning arcs, sends an EMP pulse, and electrocutes nearby mobs.
/// </summary>
public sealed partial class BluespaceCapacitorBatterySystem : EntitySystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private ElectrocutionSystem _electrocution = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BluespaceCapacitorBatteryComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnInserted(Entity<BluespaceCapacitorBatteryComponent> cell, ref EntGotInsertedIntoContainerMessage args)
    {
        // Ignore insertions that happen during map/entity initialisation (e.g. locker fills on round start).
        if (MetaData(cell.Owner).EntityLifeStage < EntityLifeStage.MapInitialized)
            return;

        var host = args.Container.Owner;

        // Allowed: capacitor gloves — designed to hold this cell.
        if (HasComp<CapacitorGlovesComponent>(host))
            return;

        // Allowed: any charger — designed to charge it.
        if (HasComp<Content.Shared.Power.Components.ChargerComponent>(host))
            return;

        // Only overcharge if the host actually tries to consume the cell as power.
        // Plain containers (backpacks, lockers, pockets, etc.) are fine.
        if (!HasComp<PowerCellSlotComponent>(host))
            return;

        // The host is a power-consuming device and can't handle the bluespace cell → overcharge.
        TriggerOvercharge(cell, host);
    }

    private void TriggerOvercharge(Entity<BluespaceCapacitorBatteryComponent> cell, EntityUid host)
    {
        var xform = Transform(cell.Owner);
        var coords = _transform.GetMapCoordinates(xform);

        // Drain the host's battery to zero.
        if (TryComp<BatteryComponent>(host, out var hostBat))
            _battery.SetCharge(new Entity<BatteryComponent?>(host, hostBat), 0f);

        // Drain the bluespace cell itself.
        if (TryComp<BatteryComponent>(cell.Owner, out var cellBat))
            _battery.SetCharge(new Entity<BatteryComponent?>(cell.Owner, cellBat), 0f);

        // EMP pulse (disables electronics, drains nearby batteries).
        _emp.EmpPulse(coords, 3f, 20000f, TimeSpan.FromSeconds(5));

        // Lightning arcs out from the overcharging cell.
        _lightning.ShootRandomLightnings(cell.Owner, 4f, 5);

        // Electrocute mobs nearby.
        var nearby = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(xform.Coordinates, 2f, nearby);
        foreach (var uid in nearby)
        {
            _electrocution.TryDoElectrocution(uid.Owner, cell.Owner, 20, TimeSpan.FromSeconds(5), true, ignoreInsulation: true);
        }
    }
}
