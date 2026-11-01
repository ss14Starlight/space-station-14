using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Starlight.Shuttles.BUIStates;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
namespace Content.Server._Starlight.Shuttles.Systems;

public sealed partial class RadarDockingStateSystem : EntitySystem
{
    [Dependency] private ShuttleConsoleSystem _shuttleConsole = null!;

    private DockingPortStates? _cachedDockingPortStates;
    private readonly Dictionary<EntityUid, long> _lastDockingPortStateVersions = new();
    private readonly Dictionary<EntityUid, (long Version, long DeliveryId)> _pendingDockingPortStateVersions = new();
    private long _dockingPortStateVersion = 1;
    private long _dockingPortStateDeliveryId;

    /// <summary>
    /// Returns the full docking-port snapshot when the console has not acknowledged the current version.
    /// </summary>
    /// <param name="console">The radar console requesting the snapshot.</param>
    /// <returns>The snapshot, or <see langword="null"/> if this console acknowledged the current version.</returns>
    /// <remarks>
    /// When a snapshot is returned, its version and delivery ID are recorded as pending for this console.
    /// The version is considered delivered only after a matching client acknowledgment.
    /// </remarks>
    public DockingPortStates? GetDockingPortStates(EntityUid console)
    {
        if (_lastDockingPortStateVersions.TryGetValue(console, out var version) &&
            version == _dockingPortStateVersion)
            return null;

        _cachedDockingPortStates ??= _shuttleConsole.GetDockingPortStates();
        if (!_pendingDockingPortStateVersions.TryGetValue(console, out var pending) ||
            pending.Version != _dockingPortStateVersion)
        {
            pending = (_dockingPortStateVersion, ++_dockingPortStateDeliveryId);
            _pendingDockingPortStateVersions[console] = pending;
        }

        return new DockingPortStates(_cachedDockingPortStates.Docks)
        {
            Version = pending.Version,
            DeliveryId = pending.DeliveryId,
        };
    }

    internal void InvalidateDockingPortStates()
    {
        _cachedDockingPortStates = null;
        _dockingPortStateVersion++;
    }

    [SubscribeLocalEvent]
    private void OnDock(DockEvent args) => InvalidateDockingPortStates();

    [SubscribeLocalEvent]
    private void OnUndock(UndockEvent args) => InvalidateDockingPortStates();

    [SubscribeLocalEvent]
    private void OnEntityRenamed(ref EntityRenamedEvent args)
    {
        if (HasComp<DockingComponent>(args.Uid))
            InvalidateDockingPortStates();
    }

    [SubscribeLocalEvent]
    private void OnDockingPortStatesAcknowledged(
        EntityUid uid,
        RadarConsoleComponent component,
        ref DockingPortStatesAcknowledgedMessage args)
    {
        if (!_pendingDockingPortStateVersions.TryGetValue(uid, out var pending) ||
            pending.Version != args.Version ||
            pending.DeliveryId != args.DeliveryId)
            return;

        _pendingDockingPortStateVersions.Remove(uid);
        if (args.Version == _dockingPortStateVersion)
            _lastDockingPortStateVersions[uid] = args.Version;
    }

    [SubscribeLocalEvent]
    private void OnRadarConsoleShutdown(EntityUid uid, RadarConsoleComponent component, ComponentShutdown args) =>
        RemoveConsoleState(uid);

    [SubscribeLocalEvent]
    private void OnRadarConsoleUiOpened(Entity<RadarConsoleComponent> entity, ref BoundUIOpenedEvent args)
    {
        if ((RadarConsoleUiKey)args.UiKey != RadarConsoleUiKey.Key)
            return;

        RemoveConsoleState(entity.Owner);
    }

    private void RemoveConsoleState(EntityUid console)
    {
        _lastDockingPortStateVersions.Remove(console);
        _pendingDockingPortStateVersions.Remove(console);
    }
}
