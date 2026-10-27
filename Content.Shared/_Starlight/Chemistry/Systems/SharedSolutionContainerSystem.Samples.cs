using Content.Shared._Starlight.Chemistry.Events;
using Content.Shared.Chemistry.Components;

namespace Content.Shared.Chemistry.EntitySystems;

public abstract partial class SharedSolutionContainerSystem
{
    partial void AfterSolutionSplit(Entity<SolutionComponent> source, Solution sample)
        => PrepareExternalSample(GetSolutionOwner(source), sample);

    /// <summary>
    /// Lets the source's handlers prepare a detached sample for external use.
    /// </summary>
    /// <param name="source">The owning entity whose sample policy should apply, not its solution entity.</param>
    /// <param name="sample">A detached solution to mutate in place; never pass an attached source solution.</param>
    /// <remarks>
    /// <see cref="SplitSolution"/> invokes this through its split hook. Callers that clone or synthesize
    /// samples must invoke it explicitly while the source still exists, before exposing the result.
    /// This only raises <see cref="ExternalSolutionSampleEvent"/>; it does not drain the source,
    /// run reactions, or notify a destination container of changes.
    /// </remarks>
    public void PrepareExternalSample(EntityUid source, Solution sample)
    {
        var ev = new ExternalSolutionSampleEvent(sample);
        RaiseLocalEvent(source, ref ev);
    }
}
