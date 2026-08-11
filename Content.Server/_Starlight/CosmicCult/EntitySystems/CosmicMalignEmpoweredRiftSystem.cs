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

namespace Content.Server._Starlight.CosmicCult;

public sealed class CosmicMalignEmpoweredRiftSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TemperatureSystem _tempSys = default!;

    public int StoredCorpseCount { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicMalignEmpoweredRiftComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CosmicMalignEmpoweredRiftComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnTerminating(Entity<CosmicMalignEmpoweredRiftComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.CorpseContainer.Count == 0)
            return;

        var corpse = ent.Comp.CorpseContainer.ContainedEntities[0];

        _container.Remove(corpse, ent.Comp.CorpseContainer);

        var riftCoordinates = Transform(ent.Owner).Coordinates;
        _transform.SetCoordinates(corpse, riftCoordinates);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var corpseCount = 0;

        var query = EntityQueryEnumerator<CosmicMalignEmpoweredRiftComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var rift, out var xform))
        {

            if (rift.CorpseContainer.Count > 0)
            {
                corpseCount++;

                var corpse = rift.CorpseContainer.ContainedEntities[0];

                if (TryComp<TemperatureComponent>(corpse, out var temperature))
                {
                    var temperatureDifference = temperature.CurrentTemperature;

                    if (temperatureDifference > 0)
                    {
                        var heatCapacity = _tempSys.GetHeatCapacity(corpse, temperature);

                        var heatToRemove =
                            temperatureDifference *
                            heatCapacity *
                            rift.CoolingCoefficient *
                            frameTime;

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
                    break;
            }
        }
        StoredCorpseCount = corpseCount;
    }

    private void OnStartup(
        Entity<CosmicMalignEmpoweredRiftComponent> ent,
        ref ComponentStartup args)
    {
        ent.Comp.CorpseContainer = _container.EnsureContainer<Container>(
            ent.Owner,
            CosmicMalignEmpoweredRiftComponent.CorpseContainerId);

        // Bodies stored inside the rift must not rot.
        EnsureComp<AntiRottingContainerComponent>(ent.Owner);
    }
}
