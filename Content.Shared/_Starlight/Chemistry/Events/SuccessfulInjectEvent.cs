using Content.Shared.Chemistry.Components;

namespace Content.Shared._Starlight.Chemistry.Events;

/// <summary>
/// Raised on the target after an injector successfully transfers solution into it.
/// </summary>
public sealed class SuccessfulInjectEvent(EntityUid user, EntityUid usedInjector, EntityUid target, Solution transferredSolution)
    : EntityEventArgs
{
    public readonly EntityUid EntityUsingInjector = user;
    public readonly EntityUid UsedInjector = usedInjector;
    public readonly EntityUid TargetGettingInjected = target;
    public readonly Solution TransferredSolution = transferredSolution;
}
