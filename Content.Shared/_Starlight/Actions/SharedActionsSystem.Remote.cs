using Content.Shared.Actions.Events;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Actions;

public abstract partial class SharedActionsSystem
{
    // allows the remote-control system to execute an action for its controlled body.
    public bool TryPerformAction(EntityUid user, EntityUid action)
        => TryPerformAction(new RequestPerformActionEvent(GetNetEntity(action)), user);

    public bool TryPerformAction(EntityUid user, EntityUid action, EntityUid? target, NetCoordinates coordinates)
        => TryPerformAction(new RequestPerformActionEvent(
            GetNetEntity(action),
            target is { } targetEntity ? GetNetEntity(targetEntity) : null,
            coordinates), user);
}
