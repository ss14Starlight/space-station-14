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
    [Dependency] private EntityLookupSystem _lookup = default!;

    /// <summary>
    /// On map init, find alert level of grid.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMapInit(Entity<AlertLevelAccessComponent> ent, ref MapInitEvent ev)
    {
        var (level, color) = FindAlertLevel(ent, Transform(ent));
        UpdateEntityAlertLevel(ent, level, color);
    }

    /// <summary>
    /// When unanchored, unsets alert level. When anchored, finds alert level from grid.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnAnchorStateChanged(Entity<AlertLevelAccessComponent> ent, ref AnchorStateChangedEvent ev)
    {
        // If entity was unanchored, unset level.
        if (!ev.Anchored)
        {
            UpdateEntityAlertLevel(ent, null, null);
            return;
        }

        var (level, color) = FindAlertLevel(ent, Transform(ent));
        UpdateEntityAlertLevel(ent, level, color);
    }

    /// <summary>
    /// When station changes alert level, finds all grids, updates relevant entities on each grid with the new alert.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnAlertLevelChange(AlertLevelChangedEvent ev)
    {
        // Station needs to be a station (duh) and needs to have an alert level before we can do anything.
        if (!TryComp<StationDataComponent>(ev.Station, out var station) ||
            !TryComp<AlertLevelComponent>(ev.Station, out var alert))
            return;

        var level = alert.CurrentLevel; // No, this line isn't redundant. Type inference gets mad otherwise.
        var color = GetAlertColor(alert);

        // Update the entities on all affected station grids.
        foreach (var grid in station.Grids)
            UpdateEntitiesOnGrid(grid, level, color);
    }

    /// <summary>
    /// When a grid becomes member of a station, we fill in the alert level and color on all relevant entities on said grid.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnStationGridAddedEvent(StationGridAddedEvent ev)
    {
        // If we can't determine alert level then there's no point.
        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert))
            return;

        var level = alert.CurrentLevel; // No, this line isn't redundant. Type inference gets mad otherwise.
        var color = GetAlertColor(alert);
        UpdateEntitiesOnGrid(ev.GridId, level, color);
    }

    /// <summary>
    /// When a grid stops being a member of a station, we wipe all level tracking state on entities on said grid.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnStationGridRemovedEvent(StationGridRemovedEvent ev)
        => UpdateEntitiesOnGrid(ev.GridId, null, null);

    /// <summary>
    /// Finds all entities with <see cref="AlertLevelAccessComponent"/> on the grid and updates them with the
    /// given level and color.
    /// </summary>
    private void UpdateEntitiesOnGrid(EntityUid grid, string? level, Color? color)
    {
        var children = new HashSet<Entity<AlertLevelAccessComponent>>();
        _lookup.GetChildEntities(grid, children);

        foreach (var child in children)
        {
            var xform = Transform(child);
            if (!xform.Anchored)
                continue;

            UpdateEntityAlertLevel(child, level, color);
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

    /// <summary>
    /// Work from a single entity upwards to find the relevant alert level and color, if any.
    /// </summary>
    private (string?, Color?) FindAlertLevel(Entity<AlertLevelAccessComponent> ent, TransformComponent? xform)
    {
        if (!Resolve(ent, ref xform))
            return (null, null);

        // Entity is not on grid -> no alert level.
        if (xform.GridUid == null)
            return (null, null);

        // Find the Station the grid belongs to and said station's alert level comp.
        var station = FindStation(ent, ref xform);
        if (!station.HasValue ||
            !TryComp<AlertLevelComponent>(station.Value, out var alert))
            return (null, null);

        // Grab the alert color.
        var color = GetAlertColor(alert);
        return (alert.CurrentLevel, color);
    }

    private void UpdateEntityAlertLevel(Entity<AlertLevelAccessComponent> ent, string? level, Color? levelColor)
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

    private Color GetAlertColor(AlertLevelComponent alerts)
    {
        if (alerts.AlertLevels == null ||
            !alerts.AlertLevels.Levels.TryGetValue(alerts.CurrentLevel, out var details))
            return Color.White;

        return details.Color;
    }
}
