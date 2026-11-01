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
    private long _dockingPortStateVersion = 1;

    public DockingPortStates? GetDockingPortStates(EntityUid console)
    {
        if (_lastDockingPortStateVersions.TryGetValue(console, out var version) &&
            version == _dockingPortStateVersion)
            return null;

        _cachedDockingPortStates ??= _shuttleConsole.GetDockingPortStates();
        _lastDockingPortStateVersions[console] = _dockingPortStateVersion;
        return _cachedDockingPortStates;
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
    private void OnRadarConsoleShutdown(EntityUid uid, RadarConsoleComponent component, ComponentShutdown args) =>
        _lastDockingPortStateVersions.Remove(uid);

    [SubscribeLocalEvent]
    private void OnRadarConsoleUiOpened(Entity<RadarConsoleComponent> entity, ref BoundUIOpenedEvent args)
    {
        if ((RadarConsoleUiKey)args.UiKey != RadarConsoleUiKey.Key)
            return;

        _lastDockingPortStateVersions.Remove(entity.Owner);
    }
}
