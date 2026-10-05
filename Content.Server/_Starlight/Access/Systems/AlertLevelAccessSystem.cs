using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Access.Components;
using Content.Shared._Starlight.Access.Systems;
using Content.Shared.Station.Components;

namespace Content.Server._Starlight.Access.Systems;

/// <summary>
/// Server side of AlertLevelAccessSystem. This propagates alert level changes to all AlertLevelAccessComponents.
/// </summary>
public sealed partial class AlertLevelAccessSystem : SharedAlertLevelAccessSystem
{
    [Dependency] private StationSystem _station = default!;

    /// <summary>
    /// On map init, find alert level of grid.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMapInit(Entity<AlertLevelAccessComponent> ent, ref MapInitEvent ev)
    {
        var xform = Transform(ent);
        var (level, color) = FindAlertLevel(ent, ref xform);
        UpdateAlertLevel(ent, level, color);
    }

    /// <summary>
    /// When unanchored, unsets alert level. When anchored, finds alert level from grid.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    [SubscribeLocalEvent]
    private void OnAnchorStateChanged(Entity<AlertLevelAccessComponent> ent, ref AnchorStateChangedEvent ev)
    {
        // If entity was unanchored, unset level.
        if (!ev.Anchored)
        {
            UpdateAlertLevel(ent);
            return;
        }

        // Otherwise actually try finding the alert level of the grid.
        var xform = Transform(ent);
        var (level, color) = FindAlertLevel(ent, ref xform);
        UpdateAlertLevel(ent, level, color);
    }

    /// <summary>
    /// Update the "current alert level" field on the <see cref="AlertLevelAccessComponent"/>s that are shared with clients.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnAlertLevelChange(AlertLevelChangedEvent ev)
    {
        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert))
            return;

        var query = EntityQueryEnumerator<AlertLevelAccessComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var alertLevelAccess, out var xform))
        {
            if (!xform.Anchored)
                continue;
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != ev.Station)
                continue;

            // Grab the alert color.
            var color = GetAlertColor(ev.Station, ref alert);
            UpdateAlertLevel((uid, alertLevelAccess), alert?.CurrentLevel, color);
        }
    }

    EntityUid? FindStation(EntityUid uid, ref TransformComponent? xform)
    {
        if (!Resolve(uid, ref xform))
            return null;

        // Entity is not on grid -> no alert level.
        if (xform.GridUid == null)
            return null;

        // Find what Station the grid belongs to
        return _station.GetOwningStation(uid);
    }

    private (string?, Color?) FindAlertLevel(Entity<AlertLevelAccessComponent> ent, ref TransformComponent? xform)
    {
        if (!Resolve(ent, ref xform))
            return (null, null);

        // Entity is not on grid -> no alert level.
        if (xform.GridUid == null)
            return (null, null);

        // Find the Station the grid belongs to and said station's alert level comp.
        var station = FindStation(ent, ref xform);
        if (!station.HasValue ||
            !TryComp<AlertLevelComponent>(station.Value, out var alerts))
            return (null, null);

        // Grab the alert color.
        var color = GetAlertColor(station.Value, ref alerts);
        return (alerts?.CurrentLevel, color);
    }

    private void UpdateAlertLevel(Entity<AlertLevelAccessComponent> ent, string? level = null, Color? levelColor = null)
    {
        // Unchanged alert level, don't write or dirty.
        if (ent.Comp.Level == level)
            return;

        // Write level and color, and dirty fields.
        ent.Comp.Level = level;
        ent.Comp.LevelColor = levelColor ?? Color.White;
        DirtyFields(ent, ent.Comp, null,
            nameof(AlertLevelAccessComponent.Level), nameof(AlertLevelAccessComponent.LevelColor));
    }

    private Color GetAlertColor(EntityUid station, ref AlertLevelComponent? alerts)
    {
        if (!Resolve(station, ref alerts) ||
            alerts.AlertLevels == null ||
            !alerts.AlertLevels.Levels.TryGetValue(alerts.CurrentLevel, out var details))
            return Color.White;

        return details.Color;
    }
}
