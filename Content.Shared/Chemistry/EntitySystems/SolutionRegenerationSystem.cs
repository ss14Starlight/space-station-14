using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed class SolutionRegenerationSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionRegenerationComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SolutionRegenerationComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    private void OnMapInit(Entity<SolutionRegenerationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextRegenTime = _timing.CurTime + ent.Comp.Duration;

        Dirty(ent);
    }

    // Workaround for https://github.com/space-wizards/space-station-14/pull/35314
    private void OnEntRemoved(Entity<SolutionRegenerationComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // Make sure the removed entity was our contained solution and clear our cached reference
        if (args.Entity == ent.Comp.SolutionRef?.Owner)
            ent.Comp.SolutionRef = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Starlight start
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<SLActiveSolutionRegenerationComponent, SolutionRegenerationComponent, SolutionContainerManagerComponent>();
        while (query.MoveNext(out var uid, out _, out var regen, out var manager))
        {
            if (time < regen.NextRegenTime)
                continue;

            // timer ignores if its full, it's just a fixed cycle
            // not anymore in Starlight! because I don't want to tick thousands of entities with this shit!
            regen.NextRegenTime = time + regen.Duration;
            // Needs to be networked and dirtied so that the client can reroll it during prediction
            Dirty(uid, regen);
            if (!_solutionContainer.ResolveSolution((uid, manager),
                    regen.SolutionName,
                    ref regen.SolutionRef,
                    out var solution))
                continue;

            var amount = FixedPoint2.Min(solution.AvailableVolume, regen.Generated.Volume);
            if (amount <= FixedPoint2.Zero)
            {
                RemCompDeferred<SLActiveSolutionRegenerationComponent>(uid);
                continue;
            }
            // Starlight end

            // Don't bother cloning and splitting if adding the whole thing
            var generated = amount == regen.Generated.Volume
                ? regen.Generated
                : regen.Generated.Clone().SplitSolution(amount);

            _solutionContainer.TryAddSolution(regen.SolutionRef.Value, generated);
        }
    }
}
