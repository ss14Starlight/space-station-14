using Content.Server.Administration.Managers;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Administration;
using Robust.Server.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

/// <summary>
/// This handles the administrative test arena maps, and loading them.
/// </summary>
public sealed partial class AdminTestArenaSystem : EntitySystem
{
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _metaDataSystem = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    #region Starlight
    [Dependency] private IServerNetConfigurationManager _cfg = null!;
    [Dependency] private IAdminManager _admin = null!;
    #endregion

    // Replaces Test Arena with a STARLIGHT varient
    public const string ArenaMapPath = "/Maps/_Starlight/Test/SL_admin_test_arena.yml";

    public Dictionary<NetUserId, EntityUid> ArenaMap { get; private set; } = new();

    // Starlight begin - No need for grid tbh + add stuff for loading custom map.
    public EntityUid AssertArenaLoaded(ICommonSession admin)
    {
        if (ArenaMap.TryGetValue(admin.UserId, out var arenaMap) && !Deleted(arenaMap) && !Terminating(arenaMap))
            return arenaMap;

        var path = _cfg.GetClientCVar(admin.Channel, StarlightCCVars.AdminArenaMapPath);
        var init = !_admin.HasAdminFlag(admin, AdminFlags.Mapping) ||
            _cfg.GetClientCVar(admin.Channel, StarlightCCVars.AdminArenaMapInit);
        // Admins must have mapping perms to utilize this.
        if (!string.IsNullOrWhiteSpace(path) && _admin.HasAdminFlag(admin, AdminFlags.Mapping))
        {
            var mapPath = new ResPath(path);
            if (!_loader.TryLoadMap(mapPath, out var map, out var grids,
                    new DeserializationOptions { InitializeMaps = init }))
            {
                QueueDel(map);
                throw new Exception($"Failed to load admin arena");
            }

            ArenaMap[admin.UserId] = map.Value;
            _metaDataSystem.SetEntityName(map.Value, $"ATAM-{admin.Name}");

            foreach (var mapGrid in grids)
                _metaDataSystem.SetEntityName(mapGrid, $"ATAG-{admin.Name}");

            return map.Value;
        }

        path = ArenaMapPath;
        var gridPath = new ResPath(path);
        var mapUid = _maps.CreateMap(out var mapId, init);

        if (!_loader.TryLoadGrid(mapId, gridPath, out var grid)) // Starlight edit
        {
            QueueDel(mapUid);
            throw new Exception($"Failed to load admin arena");
        }

        ArenaMap[admin.UserId] = mapUid;
        _metaDataSystem.SetEntityName(mapUid, $"ATAM-{admin.Name}");

        _metaDataSystem.SetEntityName(grid.Value.Owner, $"ATAG-{admin.Name}");

        return mapUid;
    }
    // Starlight end
}
