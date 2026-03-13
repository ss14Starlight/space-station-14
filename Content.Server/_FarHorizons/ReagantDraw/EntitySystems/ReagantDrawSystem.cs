using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Timing;
using Robust.Shared.Containers;
using Content.Shared._FarHorizons.ReagantDraw.Components;
using Content.Shared.Destructible;

namespace Content.Server._FarHorizons.ReagantDraw.EntitySystems;

public sealed class ReagantDrawSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReagantDrawComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReagantDrawComponent, SolutionTransferAttemptEvent>(OnSolutionTransferAttempt);
        SubscribeLocalEvent<ReagantDrawComponent, BreakageEventArgs>(OnBreakageEvent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<ReagantDrawComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled)
                continue;

            if (_timing.CurTime < comp.NextUpdateTime)
                continue;
            
            comp.NextUpdateTime = _timing.CurTime + comp.Delay;
            if(TryUseReagant(uid, comp.DrainRate, comp))
                continue;
            
            var ev = new ReagantContainerSlotEmptyEvent();
            RaiseLocalEvent(uid, ref ev);
        }
    }

    private void OnMapInit(Entity<ReagantDrawComponent> ent, ref MapInitEvent args) => 
        ent.Comp.NextUpdateTime = _timing.CurTime + ent.Comp.Delay;

    public bool TryUseReagant(EntityUid uid, float value, ReagantDrawComponent? reagantComp = null)
    {
        if (!Resolve(uid, ref reagantComp, false))
            return false;

        if(!_solutionContainer.ResolveSolution(uid, reagantComp.SolutionContainer, ref reagantComp.Solution, out var solution)
        || value > solution.Volume) 
            return false;

        UseReagant(uid, value, solution, reagantComp);
        return true;
    }

    private float UseReagant(EntityUid uid, float value, Solution solution, ReagantDrawComponent? reagantComp = null)
    {
        if (value <= 0 || !Resolve(uid, ref reagantComp) || solution.Volume == 0)
            return 0;

        return ChangeReagant(uid, value, solution, reagantComp);
    }

    public float ChangeReagant(EntityUid uid, float value, Solution solution, ReagantDrawComponent? reagantComp = null)
    {
        if (!Resolve(uid, ref reagantComp))
            return 0;
    
        solution.RemoveSolution(value);

        if( _container.TryGetContainer(uid, $"solution@{reagantComp.SolutionContainer}", out var solutionContainer) &&
            solutionContainer is ContainerSlot solutionSlot &&
            solutionSlot.ContainedEntity is { } containedSolution && TryComp<SolutionComponent>(containedSolution, out var solutionComp))
        {
            Dirty(containedSolution, solutionComp);
        }

        var ev = new ReagantChangedEvent(solution.Volume.Float(), solution.MaxVolume.Float());
        var ev2 = new SolutionContainerChangedEvent();
        RaiseLocalEvent(uid, ref ev);
        RaiseLocalEvent(uid, ref ev2);

        return solution.Volume.Float();
    }

    public bool HasDrawReagant(
        EntityUid uid,
        ReagantDrawComponent? reagantComp = null)
    {
        if (!Resolve(uid, ref reagantComp, false))
            return true;

        return HasReagant(uid, reagantComp.DrainRate, reagantComp);
    }
    
    public bool HasReagant(EntityUid uid, float charge, ReagantDrawComponent reagantComp)
    {
        if(!_solutionContainer.ResolveSolution(uid, reagantComp.SolutionContainer, ref reagantComp.Solution, out var solution)) 
            return false;

        if (solution.Volume < charge)
            return false;

        return true;
    }

    private void OnSolutionTransferAttempt(Entity<ReagantDrawComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if(ent.Comp.WhitelistedReagants.Count == 0) return;

        var solution = args.SolutionEntity.Comp.Solution;
        if (solution.Contents.Any(sol => !ent.Comp.WhitelistedReagants.Any(req => req.Id == sol.Reagent.ToString())))
        {
            args.Cancel("This solution isn't the right solution!");
            return;
        }
    }

    private void OnBreakageEvent(EntityUid ent, ReagantDrawComponent component, BreakageEventArgs args)
    {
        if(!_solutionContainer.ResolveSolution(ent, component.SolutionContainer, ref component.Solution, out var solution)) return;

        UseReagant(ent, solution.Volume.Float(), solution, component);
    }
}
