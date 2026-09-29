using Content.Server.Shuttles.Components;

namespace Content.Server.Shuttles.Events;

/// <summary>
/// Raised whenever 2 grids undock.
/// </summary>
public sealed class UndockEvent : EntityEventArgs
{
    // Starlight-start
    public Entity<DockingComponent> DockA = default!;
    public Entity<DockingComponent> DockB = default!;
    // Starlight-end

    public EntityUid GridAUid = default!;
    public EntityUid GridBUid = default!;
}
