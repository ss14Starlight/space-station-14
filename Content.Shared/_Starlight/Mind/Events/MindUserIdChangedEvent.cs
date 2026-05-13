using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Mind.Events;

/// <summary>
/// Raised when the user associated with a mind changes.
/// Raised on the owning mind-container body.
/// </summary>
public sealed class MindUserIdChangedEvent(
    Entity<MindComponent> mind,
    Entity<MindContainerComponent> container,
    NetUserId? oldUserId,
    NetUserId? newUserId) : EntityEventArgs
{
    public readonly Entity<MindComponent> Mind = mind;
    public readonly Entity<MindContainerComponent> Container = container;
    public readonly NetUserId? OldUserId = oldUserId;
    public readonly NetUserId? NewUserId = newUserId;
}
