using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.Storage;

namespace Content.Shared._Starlight.Fluids.DrainingStorage;

public sealed partial class DrainingStorageSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<DrainingStorageComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<DrainingStorageComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        }
    private void OnMapInit(Entity<DrainingStorageComponent> ent, ref MapInitEvent args)
    {

        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.DrainInterval;
        Dirty(ent);

        if (!TryComp<StorageComponent>(ent, out var storage))
            return;

        var stored = storage.Container.ContainedEntities;

        foreach (var container in stored)
        {
            Drain(ent, container);
        }
    }
    private void OnEntInserted(Entity<DrainingStorageComponent> ent, ref EntInsertedIntoContainerMessage args) => Drain(ent, args.Entity);

    private void Drain(Entity<DrainingStorageComponent> buffer, EntityUid container)
    {
        if (!_solutionContainerSystem.TryGetDrainableSolution(container, out var containerSoln, out var containerSolution) || containerSolution.Volume == FixedPoint2.Zero)
            return;
        if (!_solutionContainerSystem.ResolveSolution(buffer.Owner, DrainingStorageComponent.SolutionName, ref buffer.Comp.Solution, out var bufferSolution))
            return;
        var amountToPutInBuffer = bufferSolution.AvailableVolume;
        var amountToSpillOnGround = containerSolution.Volume - bufferSolution.AvailableVolume;

        if (amountToPutInBuffer > 0)
        {
            var solutionToPutInBuffer = _solutionContainerSystem.SplitSolution(containerSoln.Value, amountToPutInBuffer);
            _solutionContainerSystem.TryAddSolution(buffer.Comp.Solution.Value, solutionToPutInBuffer);
        }

        if (amountToSpillOnGround > 0)
        {
            var solutionToSpill = _solutionContainerSystem.SplitSolution(containerSoln.Value, amountToSpillOnGround);
            _puddle.TrySpillAt(Transform(buffer.Owner).Coordinates, solutionToSpill, out _);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DrainingStorageComponent>();
        var curTime = _timing.CurTime;
        while (query.MoveNext(out var uid, out var buffer))
        {
            if (curTime < buffer.NextUpdate)
                continue;

            buffer.NextUpdate = curTime + buffer.DrainInterval;
            Dirty(uid, buffer);

            if (!_solutionContainerSystem.ResolveSolution(uid, DrainingStorageComponent.SolutionName, ref buffer.Solution, out var bufferSolution))
                continue;

            if (bufferSolution.Volume <= FixedPoint2.Zero)
                continue;

            _solutionContainerSystem.SplitSolution(buffer.Solution.Value, buffer.UnitsDrainedPerSecond * buffer.DrainInterval.TotalSeconds);

            _solutionContainerSystem.UpdateChemicals(buffer.Solution.Value);
        }
    }
}
