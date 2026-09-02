using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Station.Components;
using Content.Shared.UserInterface;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Maps;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    #region Starlight
    [Dependency] private SharedGridAccessSystem _gridAccess = default!;
    private readonly Dictionary<EntityUid, (EntityUid SourceGrid, EntityUid TargetGrid)> _remoteGridAccess = new();

    private void OnDroneConsoleStartup(EntityUid uid, DroneConsoleComponent component, ComponentStartup args)
    {
        UpdateRemoteGridAccess(uid, component);
    }
    #endregion Starlight

    /// <summary>
    /// Gets the drone console target if applicable otherwise returns itself.
    /// </summary>
    public EntityUid? GetDroneConsole(EntityUid consoleUid)
    {
        var getShuttleEv = new ConsoleShuttleEvent
        {
            Console = consoleUid,
        };

        RaiseLocalEvent(consoleUid, ref getShuttleEv);
        return getShuttleEv.Console;
    }

    /// <summary>
    /// Refreshes all drone console entities.
    /// </summary>
    public void RefreshDroneConsoles()
    {
        var query = AllEntityQuery<DroneConsoleComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateRemoteGridAccess(uid, comp); // Starlight
        }
    }

    private void OnDronePilotConsoleOpen(EntityUid uid, DroneConsoleComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateRemoteGridAccess(uid, component); // Starlight
    }

    private void OnDronePilotConsoleClose(EntityUid uid, DroneConsoleComponent component, BoundUIClosedEvent args)
    {
        // Only if last person closed UI.
        if (!_ui.IsUiOpen(uid, args.UiKey))
        {
            component.Entity = null;
            _remoteGridAccess.Remove(uid); // We only remove the access to the remote grid, not changing grid access
        }
    }

    private void OnCargoGetConsole(EntityUid uid, DroneConsoleComponent component, ref ConsoleShuttleEvent args)
    {
        UpdateRemoteGridAccess(uid, component);  // Starlight
        args.Console = component.Entity;
    }

    #region Starlight
    private void UpdateRemoteGridAccess(EntityUid uid, DroneConsoleComponent component)
    {
        var targetConsole = GetShuttleConsole(uid, component);
        var sourceGrid = Transform(uid).GridUid;
        var targetGrid = targetConsole is { } target ? Transform(target).GridUid : null;

        if (_remoteGridAccess.TryGetValue(uid, out var previous)
            && (sourceGrid != previous.SourceGrid || targetGrid != previous.TargetGrid
            || !IsConsoleOperational(uid) || targetConsole is not { } currentTarget || !IsConsoleOperational(currentTarget)))
        {
            RemoveRemoteGridAccess(uid);
        }

        component.Entity = targetConsole;

        if (sourceGrid is not { } source || targetGrid is not { } targetUid ||
            targetConsole is not { } console ||
            !IsConsoleOperational(uid) || !IsConsoleOperational(console))
        {
            return;
        }

        if (_remoteGridAccess.ContainsKey(uid))
            return;

        _gridAccess.AddAccessibleGrid((source, null), (targetUid, null));
        _remoteGridAccess[uid] = (source, targetUid);
    }

    private void RemoveRemoteGridAccess(EntityUid uid)
    {
        if (!_remoteGridAccess.Remove(uid, out var access))
            return;

        foreach (var other in _remoteGridAccess.Values)
        {
            if (other != access)
                continue;

            return;
        }

        _gridAccess.RemoveAccessibleGrid((access.SourceGrid, null), (access.TargetGrid, null));
    }

    private bool IsConsoleOperational(EntityUid uid)
    {
        return TryComp<ShuttleConsoleComponent>(uid, out _) && MetaData(uid).EntityLifeStage < EntityLifeStage.Terminating && Transform(uid).Anchored && this.IsPowered(uid, EntityManager);
    }
    #endregion Starlight

    /// <summary>
    /// Gets the relevant shuttle console to proxy from the drone console.
    /// </summary>
    private EntityUid? GetShuttleConsole(EntityUid uid, DroneConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return null;

        var stationUid = _station.GetOwningStation(uid);

        if (stationUid == null)
            return null;

        // I know this sucks but needs device linking or something idunno
        var query = AllEntityQuery<ShuttleConsoleComponent, TransformComponent>();

        while (query.MoveNext(out var cUid, out _, out var xform))
        {
            if (xform.GridUid == null ||
                !TryComp<StationMemberComponent>(xform.GridUid, out var member) ||
                member.Station != stationUid)
            {
                continue;
            }

            foreach (var compType in component.Components.Values)
            {
                if (!HasComp(xform.GridUid, compType.Component.GetType()))
                    continue;

                return cUid;
            }
        }

        return null;
    }
}
