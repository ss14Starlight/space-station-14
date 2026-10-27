using Content.Shared.Actions.Events;
using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Shared.Actions;

public abstract partial class SharedActionsSystem
{
    /// <summary>
    /// Performs a body-control action with an optional entity or position target.
    /// The caller must authorize the controller and action before invoking this method.
    /// </summary>
    public bool TryPerformBodyControlAction(EntityUid user, EntityUid action, EntityUid? target, NetCoordinates? coordinates)
        => TryPerformAction(coordinates is { } position
            ? new RequestPerformActionEvent(GetNetEntity(action), GetNetEntity(target), position)
            : target is { } entity ? new RequestPerformActionEvent(GetNetEntity(action), GetNetEntity(entity))
            : new RequestPerformActionEvent(GetNetEntity(action)), user);
}
