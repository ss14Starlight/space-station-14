using Content.Server._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Content.Server.Temperature.Systems;
using Content.Shared.Temperature.Components;
using Content.Server.Atmos.Components;
using Robust.Shared.Map;
using Robust.Shared.GameStates;

namespace Content.Server._Starlight.CosmicCult.EntitySystems;

public sealed partial class CosmicMalignEmpoweredRiftSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TemperatureSystem _tempSys = default!;

    public int StoredCorpseCount { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicMalignEmpoweredRiftComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CosmicMalignEmpoweredRiftComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<CosmicMalignEmpoweredRiftComponent, ComponentGetState>(OnGetState);
    }

    private void OnGetState(Entity<CosmicMalignEmpoweredRiftComponent> ent, ref ComponentGetState args) =>
    args.State = new CosmicMalignEmpoweredRiftComponent.State
    {
        IsOccupied = ent.Comp.IsOccupied,
    };

    private void OnTerminating(Entity<CosmicMalignEmpoweredRiftComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.CorpseContainer.Count == 0)
            return;

        var corpse = ent.Comp.CorpseContainer.ContainedEntities[0];
        var xform = Transform(ent.Owner);

        if (_container.Remove(corpse, ent.Comp.CorpseContainer, reparent: false, force: true))
        {
            _transform.SetCoordinates(corpse, new EntityCoordinates(xform.ParentUid, xform.LocalPosition));
            RemComp<PressureImmunityComponent>(corpse);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var corpseCount = 0;

        var query = EntityQueryEnumerator<CosmicMalignEmpoweredRiftComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var rift, out var xform))
        {
        /// <summary>
        /// Tracks corpses already stored within empowered rifts and gradually
        /// cools them toward a minimum temperature while they remain stored.
        /// </summary>
            if (rift.CorpseContainer.Count > 0)
            {
                corpseCount++;

                var corpse = rift.CorpseContainer.ContainedEntities[0];

                if (TryComp<TemperatureComponent>(corpse, out var temperature))
                {
                    var currentTemperature = temperature.CurrentTemperature;

                    if (currentTemperature > 5f)
                    {
                        var heatCapacity = _tempSys.GetHeatCapacity(corpse, temperature);
                        var temperatureDrop = currentTemperature * rift.CoolingCoefficient * frameTime;
                        var targetTemperature = Math.Max(0f, currentTemperature - temperatureDrop);
                        var heatToRemove = (currentTemperature - targetTemperature) * heatCapacity;

                        _tempSys.ChangeHeat(
                            corpse,
                            -heatToRemove,
                            ignoreHeatResistance: false,
                            temperature);
                    }
                }
            }

            // The rift can only store one corpse ever.
            if (rift.CorpseContainer.Count > 0)
                continue;

            /// <summary>
            /// Searches the rift's tile for critical or dead humanoids and stores
            /// the first valid corpse found inside the rift.
            /// </summary>
            // Find entities occupying the rift's tile.
            var entities = _lookup.GetEntitiesIntersecting(
                xform.Coordinates,
                LookupFlags.Dynamic | LookupFlags.Static);

            foreach (var target in entities)
            {
                // Don't try to eat the rift itself.
                if (target == uid)
                    continue;

                // Only humanoids can be absorbed.
                if (!HasComp<HumanoidAppearanceComponent>(target))
                    continue;

                // The humanoid must be critical or dead.
                if (!TryComp<MobStateComponent>(target, out var mobState))
                    continue;

                if (mobState.CurrentState is not (MobState.Critical or MobState.Dead))
                    continue;

                // Dont eat your own friends by accident
                if (HasComp<CosmicCultComponent>(target))
                    continue;

                // Store the corpse inside the rift.
                if (_container.Insert(target, rift.CorpseContainer))
                {
                    corpseCount++;
                    rift.IsOccupied = true;
                    Dirty(uid, rift);
                    EnsureComp<PressureImmunityComponent>(target);
                    break;
                }
            }
        }
        StoredCorpseCount = corpseCount;
    }

    private void OnStartup(Entity<CosmicMalignEmpoweredRiftComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.CorpseContainer = _container.EnsureContainer<Container>(
            ent.Owner,
            CosmicMalignEmpoweredRiftComponent.CorpseContainerId);

        // Bodies stored inside the rift must not rot.
        EnsureComp<AntiRottingContainerComponent>(ent.Owner);
    }
}
