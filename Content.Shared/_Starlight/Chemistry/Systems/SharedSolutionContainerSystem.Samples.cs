using Content.Shared._Starlight.Chemistry.Events;
using Content.Shared.Chemistry.Components;

namespace Content.Shared.Chemistry.EntitySystems;

public abstract partial class SharedSolutionContainerSystem
{
    partial void AfterSolutionSplit(Entity<SolutionComponent> source, Solution sample)
        => PrepareExternalSample(GetSolutionOwner(source), sample);

    /// <summary>
    /// Prepare a detached sample for use outside its source. Also used by callers that clone
    /// or synthesize a sample instead of removing it through <see cref="SplitSolution"/>.
    /// </summary>
    public void PrepareExternalSample(EntityUid source, Solution sample)
    {
        var ev = new ExternalSolutionSampleEvent(sample);
        RaiseLocalEvent(source, ref ev);
    }
}
