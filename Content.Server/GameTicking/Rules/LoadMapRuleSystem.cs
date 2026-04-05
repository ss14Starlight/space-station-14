using System.Linq;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.GridPreloader;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Tag; // Starlight
using Content.Shared.GameTicking.Rules; // Starlight
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components; // Starlight
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Configuration;
using Content.Shared.Starlight.CCVar;

namespace Content.Server.GameTicking.Rules;

public sealed class LoadMapRuleSystem : StationEventSystem<LoadMapRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly GridPreloaderSystem _gridPreloader = default!;
    #region Starlight
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IMapManager _maps = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly EntityManager _entMan = default!;
    [Dependency] private readonly DynamicRuleSystem _dynamicRule = default!;
    #endregion Starlight

    protected override void Added(EntityUid uid, LoadMapRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {
        if (comp.PreloadedGrid != null && !_gridPreloader.PreloadingEnabled)
        {
            // Preloading will never work if it's disabled, duh
            Log.Debug($"Immediately ending {ToPrettyString(uid):rule} as preloading grids is disabled by cvar.");
            ForceEndSelf(uid, rule);
            return;
        }

        // Starlight start
        if (_cfg.GetCVar(StarlightCCVars.DisableLoadMapRule))
            return;

        if (comp.MapTag.HasValue && LoadMapTag(uid, comp, rule, args, comp.MapTag.Value))
            return;
        // Starlight end

        MapId mapId;
        IReadOnlyList<EntityUid> grids;
        if (comp.GameMap != null)
        {
            // Component has one of three modes, only one of the three fields should ever be populated.
            DebugTools.AssertNull(comp.MapPath);
            DebugTools.AssertNull(comp.GridPath);
            DebugTools.AssertNull(comp.PreloadedGrid);

            var gameMap = _prototypeManager.Index(comp.GameMap.Value);
            grids = GameTicker.LoadGameMap(gameMap, out mapId, null);
            Log.Info($"Created map {mapId} for {ToPrettyString(uid):rule}");
        }
        else if (comp.MapPath is { } path)
        {
            DebugTools.AssertNull(comp.GridPath);
            DebugTools.AssertNull(comp.PreloadedGrid);

            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            if (!_mapLoader.TryLoadMap(path, out var map, out var gridSet, opts))
            {
                Log.Error($"Failed to load map from {path}!");
                ForceEndSelf(uid, rule);
                return;
            }

            grids = gridSet.Select(x => x.Owner).ToList();
            mapId = map.Value.Comp.MapId;

            if (comp.MapTag.HasValue)
                _tag.AddTag(map.Value, comp.MapTag.Value);
        }
        else if (comp.GridPath is { } gPath)
        {
            DebugTools.AssertNull(comp.PreloadedGrid);

            // I fucking love it when "map paths" choses to ar
            _map.CreateMap(out mapId);
            var opts = DeserializationOptions.Default with { InitializeMaps = true };
            if (!_mapLoader.TryLoadGrid(mapId, gPath, out var grid, opts))
            {
                Log.Error($"Failed to load grid from {gPath}!");
                ForceEndSelf(uid, rule);
                return;
            }

            grids = new List<EntityUid> { grid.Value.Owner };
        }
        else if (comp.PreloadedGrid is { } preloaded)
        {
            // TODO: If there are no preloaded grids left, any rule announcements will still go off!
            if (!_gridPreloader.TryGetPreloadedGrid(preloaded, out var loadedShuttle))
            {
                Log.Error($"Failed to get a preloaded grid with {preloaded}!");
                ForceEndSelf(uid, rule);
                return;
            }

            var mapUid = _map.CreateMap(out mapId, runMapInit: false);
            _transform.SetParent(loadedShuttle.Value, mapUid);
            grids = new List<EntityUid>() { loadedShuttle.Value };
            _map.InitializeMap(mapUid);
        }
        else
        {
            Log.Error($"No valid map prototype or map path associated with the rule {ToPrettyString(uid)}");
            ForceEndSelf(uid, rule);
            return;
        }

        var ev = new RuleLoadedGridsEvent(mapId, grids);
        RaiseLocalEvent(uid, ref ev);

        PropagateLoadEvent(uid, mapId, grids); // Starlight

        base.Added(uid, comp, rule, args);
    }

    /// <summary>
    /// Recursively propagate the load event up the rule tree.
    /// </summary>
    private void PropagateLoadEvent(EntityUid child, MapId mapId, IReadOnlyList<EntityUid> grids) {
        var dynamicRules = _entMan.AllEntityQueryEnumerator<DynamicRuleComponent>();
        while (dynamicRules.MoveNext(out var uid, out var comp))
        {
            if (_dynamicRule.Rules((uid, (DynamicRuleComponent?)comp)).Contains(child)) {
                var ev = new RuleLoadedGridsEvent(mapId, grids);
                RaiseLocalEvent(uid, ref ev);
                PropagateLoadEvent(uid, mapId, grids);
                break;
            }
        }
        var parentRules = _entMan.AllEntityQueryEnumerator<SubRuleComponent>();
        while (parentRules.MoveNext(out var uid, out var comp))
        {
            if (comp.Rules.Contains(child)) {
                var ev = new RuleLoadedGridsEvent(mapId, grids);
                RaiseLocalEvent(uid, ref ev);
                PropagateLoadEvent(uid, mapId, grids);
                break;
            }
        }
    }

    private bool LoadMapTag(EntityUid uid, LoadMapRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args, ProtoId<TagPrototype> MapTag)
    {
        MapId mapId;
        IReadOnlyList<EntityUid> grids;

        if (comp.MapPath is { } path)
        {
            var query = EntityQueryEnumerator<MapComponent>();
            while (query.MoveNext(out var mapuid, out var mapcomp))
            {
                if (mapcomp.MapPaused)
                    continue;

                if (_tag.HasTag(mapuid, MapTag))
                {
                    mapId = mapcomp.MapId;

                    var gridSet = _maps.GetAllGrids(mapcomp.MapId).ToList();
                    grids = gridSet.Select(x => x.Owner).ToList();

                    var ev = new RuleLoadedGridsEvent(mapId, grids);
                    RaiseLocalEvent(uid, ref ev);

                    PropagateLoadEvent(uid, mapId, grids);

                    base.Added(uid, comp, rule, args);

                    return true;
                }
            }
        }
        return false;
    }

    // Starlight end
}
