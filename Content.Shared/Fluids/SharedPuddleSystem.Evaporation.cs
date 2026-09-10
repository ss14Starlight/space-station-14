using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Content.Shared.Fluids.Components;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared.Fluids;

public abstract partial class SharedPuddleSystem
{
    private static readonly TimeSpan _evaporationCooldown = TimeSpan.FromSeconds(1);
    private TimeSpan _nextEvaporationUpdate = TimeSpan.MaxValue; // Starlight
    private readonly List<ProtoId<ReagentPrototype>> _evaporationReagents = []; // Starlight

    private void OnEvaporationMapInit(Entity<EvaporationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime + _evaporationCooldown;
        ScheduleEvaporation(ent.Comp.NextTick); // Starlight
        Dirty(ent);
    }

    private void UpdateEvaporation(EntityUid uid, Solution solution)
    {
        if (!HasEvaporatingReagent(solution)) // Starlight
        {
            RemComp<EvaporationComponent>(uid); // Starlight
            return;
        }

        if (_evaporationQuery.TryGetComponent(uid, out var existing)) // Starlight
        {
            ScheduleEvaporation(existing.NextTick); // Starlight
            return;
        }

        #region Starlight
        var evaporation = AddComp<EvaporationComponent>(uid);
        evaporation.NextTick = _timing.CurTime + _evaporationCooldown;
        ScheduleEvaporation(evaporation.NextTick);
        Dirty<EvaporationComponent>((uid, evaporation));
        #endregion
    }

    private void TickEvaporation()
    {
        var query = EntityQueryEnumerator<EvaporationComponent, PuddleComponent>();
        var curTime = _timing.CurTime;
        _nextEvaporationUpdate = TimeSpan.MaxValue; // Starlight

        while (query.MoveNext(out var uid, out var evaporation, out var puddle))
        {
            if (evaporation.NextTick > curTime)
            {
                ScheduleEvaporation(evaporation.NextTick); // Starlight
                continue;
            }

            // Necessary to keep client and server in sync so they don't drift
            evaporation.NextTick += _evaporationCooldown;
            ScheduleEvaporation(evaporation.NextTick); // Starlight
            Dirty(uid, evaporation);

            if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var puddleSolution))
                continue;

            // If we have multiple evaporating reagents in one puddle, just take the average evaporation speed and apply
            // that to all of them.
            #region Starlight
            _evaporationReagents.Clear();
            var totalEvaporationSpeed = FixedPoint2.Zero;
            foreach (var (reagent, _) in puddleSolution.Contents)
            {
                var reagentId = new ProtoId<ReagentPrototype>(reagent.Prototype);
                if (_evaporationReagents.Contains(reagentId))
                    continue;

                var speed = _prototypeManager.Index<ReagentPrototype>(reagentId).EvaporationSpeed;
                if (speed <= FixedPoint2.Zero)
                    continue;

                _evaporationReagents.Add(reagentId);
                totalEvaporationSpeed += speed;
            }

            if (_evaporationReagents.Count == 0)
                continue;
            #endregion

            var evaporationSpeed = totalEvaporationSpeed / _evaporationReagents.Count; // Starlight
            var initialVolume = puddleSolution.Volume; // Starlight

            // Still have to iterate over one-by-one since the full solution could have non-evaporating solutions.
            #region Starlight
            foreach (var reagent in _evaporationReagents)
            {
                var factor = puddleSolution.GetTotalPrototypeQuantity(reagent) / initialVolume;
                var reagentTick = evaporation.EvaporationAmount * _evaporationCooldown.TotalSeconds * evaporationSpeed * factor;
                puddleSolution.RemoveReagent(reagent, reagentTick, ignoreReagentData: true);
            }
            #endregion

            // Despawn if we're done
            if (puddleSolution.Volume == FixedPoint2.Zero)
            {
                // Spawn a *sparkle*
                if (_net.IsServer) // TODO: Change this once we have entity spawn prediction V2
                    SpawnAttachedTo(evaporation.EvaporationEffect, Transform(uid).Coordinates);
                PredictedQueueDel(uid);
            }

            _solutionContainerSystem.UpdateChemicals(puddle.Solution.Value);
        }
    }

    public ProtoId<ReagentPrototype>[] GetEvaporatingReagents(Solution solution)
    {
        List<ProtoId<ReagentPrototype>> evaporatingReagents = [];
        foreach (var solProto in solution.GetReagentPrototypes(_prototypeManager).Keys)
        {
            if (solProto.EvaporationSpeed > FixedPoint2.Zero)
                evaporatingReagents.Add(solProto.ID);
        }
        return evaporatingReagents.ToArray();
    }

    public ProtoId<ReagentPrototype>[] GetAbsorbentReagents(Solution solution)
    {
        var absorbentReagents = new List<ProtoId<ReagentPrototype>>();
        foreach (ReagentPrototype solProto in solution.GetReagentPrototypes(_prototypeManager).Keys)
        {
            if (solProto.Absorbent)
                absorbentReagents.Add(solProto.ID);
        }
        return absorbentReagents.ToArray();
    }

    public bool CanFullyEvaporate(Solution solution)
    {
        #region Starlight
        foreach (var (reagent, _) in solution.Contents)
        {
            if (_prototypeManager.Index<ReagentPrototype>(reagent.Prototype).EvaporationSpeed <= FixedPoint2.Zero)
                return false;
        }

        return true;
        #endregion
    }

    /// <summary>
    /// Gets a mapping of evaporating speed of the reagents within a solution.
    /// The speed at which a solution evaporates is the average of the speed of all evaporating reagents in it.
    /// </summary>
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> GetEvaporationSpeeds(Solution solution)
    {
        Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> evaporatingSpeeds = [];
        foreach (var solProto in solution.GetReagentPrototypes(_prototypeManager).Keys)
        {
            if (solProto.EvaporationSpeed > FixedPoint2.Zero)
            {
                evaporatingSpeeds.Add(solProto.ID, solProto.EvaporationSpeed);
            }
        }
        return evaporatingSpeeds;
    }
}
