using Content.Server.Shuttles.Components;

namespace Content.Server.Shuttles.Events;

/// <summary>
/// Raised whenever 2 airlocks dock.
/// </summary>
public sealed class DockEvent : EntityEventArgs
{
    public EntityUid DockAUid = default!;
    public EntityUid DockBUid = default!;
    public DockingComponent DockA = default!;
    public DockingComponent DockB = default!;

    public EntityUid GridAUid = default!;
    public EntityUid GridBUid = default!;
}
