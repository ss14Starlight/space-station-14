using Content.Shared.Chemistry.Components;

namespace Content.Shared._Starlight.Chemistry.Events;

/// <summary>
/// Raised on the source when a detached solution sample is prepared for use outside its owner.
/// Handlers may adjust the sample without changing the source solution.
/// </summary>
[ByRefEvent]
public readonly record struct ExternalSolutionSampleEvent(Solution Sample);
