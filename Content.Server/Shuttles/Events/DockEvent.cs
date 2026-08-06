using Content.Server.Shuttles.Components;

namespace Content.Server.Shuttles.Events;

/// <summary>
/// Raised whenever 2 airlocks dock.
/// </summary>
public sealed class DockEvent : EntityEventArgs
{
    public Entity<DockingComponent> DockA = default!;
    public Entity<DockingComponent> DockB = default!;

    public EntityUid GridAUid = default!;
    public EntityUid GridBUid = default!;
}
