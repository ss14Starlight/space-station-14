using System.Linq;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared._Starlight.ReagentDraw.Components;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.ReagentDraw.EntitySystems;

public sealed partial class SharedReagentDrawSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<ReagentDrawComponent> ent, ref MapInitEvent args)
        => ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.Delay;

    [SubscribeLocalEvent]
    private void OnTransferAttempt(Entity<ReagentDrawComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner || ent.Comp.WhitelistedReagents.Count == 0)
            return;

        if (args.SolutionEntity.Comp.Solution.Contents.Any(q => !ent.Comp.WhitelistedReagents.Contains(q.Reagent.Prototype)))
            args.Cancel(Loc.GetString("vehicle-invalid-fuel"));
    }

    [SubscribeLocalEvent]
    private void OnBreak(EntityUid owner, ReagentDrawComponent component, BreakageEventArgs args)
    {
        var ent = new Entity<ReagentDrawComponent>(owner, component);
        if (_solutions.ResolveSolution(ent.Owner, ent.Comp.SolutionContainer, ref ent.Comp.Solution, out var solution))
            _solutions.SplitSolution(ent.Comp.Solution!.Value, solution.Volume);
    }

    public void SetDrawEnabled(Entity<ReagentDrawComponent> ent, bool enabled)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        if (enabled)
            ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.Delay;
        Dirty(ent);
    }

    public bool HasDrawReagent(Entity<ReagentDrawComponent?> ent, FixedPoint2 additionalDrain = default) => Resolve(ent, ref ent.Comp, false) &&
            _solutions.ResolveSolution(ent.Owner, ent.Comp.SolutionContainer, ref ent.Comp.Solution, out var solution) &&
            solution.Volume >= FixedPoint2.Max(FixedPoint2.Epsilon, ent.Comp.DrainRate + additionalDrain);

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<ReagentDrawComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled || _timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.Delay;
            if (HasDrawReagent((uid, comp)))
            {
                var solution = comp.Solution!.Value;
                var draw = new BeforeReagentDrawEvent(solution.Comp.Solution.Volume, comp.DrainRate);
                RaiseLocalEvent(uid, ref draw);
                _solutions.SplitSolution(solution, draw.DrainAmount);
            }

            if (HasDrawReagent((uid, comp)))
                continue;

            SetDrawEnabled((uid, comp), false);
            var empty = new ReagentContainerSlotEmptyEvent();
            RaiseLocalEvent(uid, ref empty);
        }
    }
}
