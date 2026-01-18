using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Chemistry;

public sealed class SLSolutionRegenerationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SolutionRegenerationComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    private void OnSolutionChanged(Entity<SolutionRegenerationComponent> ent, ref SolutionContainerChangedEvent args)
    {
        //make sure the entity isnt terminating
        if (TerminatingOrDeleted(ent))
            return;
            
        // No component additions during client state application
        if (_timing.ApplyingState)
            return;

        if (args.Solution.AvailableVolume <= FixedPoint2.Zero)
            return;

        EnsureComp<SLActiveSolutionRegenerationComponent>(ent);
    }
}
