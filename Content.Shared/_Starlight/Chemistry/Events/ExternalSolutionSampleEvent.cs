using Content.Shared.Chemistry.Components;

namespace Content.Shared._Starlight.Chemistry.Events;

/// <summary>
/// Raised on the source when a detached solution sample is prepared for use outside its owner.
/// </summary>
/// <param name="Sample">The detached, mutable solution that will leave the source.</param>
/// <remarks>
/// The caller must split, clone, or synthesize the sample before raising this event. Handlers may adjust
/// the sample in place, but must not alter the source's attached solution. Raising the event does not
/// drain the source or insert the sample into a destination.
/// </remarks>
[ByRefEvent]
public readonly record struct ExternalSolutionSampleEvent(Solution Sample);
