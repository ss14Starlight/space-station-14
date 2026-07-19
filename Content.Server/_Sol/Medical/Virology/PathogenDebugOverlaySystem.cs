using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Server-side pathogen debug overlay observer sync, modeled on AtmosDebugOverlaySystem.
/// </summary>
public sealed class PathogenDebugOverlaySystem : SharedPathogenDebugOverlaySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;

    private readonly HashSet<ICommonSession> _playerObservers = new();
    private float _updateCooldown;
    private List<Entity<MapGridComponent>> _grids = new();

    public override void Initialize()
    {
        base.Initialize();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public bool AddObserver(ICommonSession observer) => _playerObservers.Add(observer);

    public bool HasObserver(ICommonSession observer) => _playerObservers.Contains(observer);

    public bool RemoveObserver(ICommonSession observer)
    {
        if (!_playerObservers.Remove(observer))
            return false;

        RaiseNetworkEvent(new PathogenDebugOverlayDisableMessage(), observer.Channel);
        return true;
    }

    public bool ToggleObserver(ICommonSession observer)
    {
        if (HasObserver(observer))
        {
            RemoveObserver(observer);
            return false;
        }

        AddObserver(observer);
        return true;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.InGame)
            RemoveObserver(e.Session);
    }

    public override void Update(float frameTime)
    {
        AccumulatedFrameTime += frameTime;
        _updateCooldown = 1 / _configManager.GetCVar(CCVars.NetAtmosDebugOverlayTickRate);

        if (AccumulatedFrameTime < _updateCooldown)
            return;

        AccumulatedFrameTime -= _updateCooldown;

        foreach (var session in _playerObservers)
        {
            if (session.AttachedEntity is not { Valid: true } entity)
                continue;

            var transform = Transform(entity);
            var pos = _transform.GetWorldPosition(transform);
            var worldBounds = Box2.CenteredAround(pos, new Vector2(LocalViewRange, LocalViewRange));

            _grids.Clear();
            _mapSystem.FindGridsIntersecting(transform.MapID, worldBounds, ref _grids);

            foreach (var grid in _grids)
            {
                var uid = grid.Owner;
                if (!Exists(uid))
                    continue;

                TryComp<GridPathogenAtmosphereComponent>(uid, out var store);

                var entityTile = _mapSystem.GetTileRef(grid, grid, transform.Coordinates).GridIndices;
                var baseTile = new Vector2i(entityTile.X - LocalViewRange / 2, entityTile.Y - LocalViewRange / 2);
                var overlay = new PathogenDebugOverlayTile?[LocalViewRange * LocalViewRange];

                var index = 0;
                for (var y = 0; y < LocalViewRange; y++)
                for (var x = 0; x < LocalViewRange; x++)
                {
                    var tile = new Vector2i(baseTile.X + x, baseTile.Y + y);
                    var blocked = AtmosDirection.Invalid;
                    foreach (var dir in new[]
                             {
                                 AtmosDirection.North, AtmosDirection.South, AtmosDirection.East, AtmosDirection.West,
                             })
                    {
                        if (_atmos.IsTileAirBlocked(uid, tile, dir, grid))
                            blocked |= dir;
                    }

                    if (store != null && store.Tiles.TryGetValue(tile, out var pathogens) && pathogens.Count > 0)
                    {
                        var entries = new (string, float)[pathogens.Count];
                        var i = 0;
                        var total = 0f;
                        foreach (var (id, load) in pathogens)
                        {
                            entries[i++] = (id, load);
                            total += load;
                        }

                        overlay[index++] = new PathogenDebugOverlayTile(tile, total, entries, blocked);
                    }
                    else
                    {
                        overlay[index++] = new PathogenDebugOverlayTile(tile, 0f, Array.Empty<(string, float)>(), blocked);
                    }
                }

                RaiseNetworkEvent(new PathogenDebugOverlayMessage(GetNetEntity(uid), baseTile, overlay), session.Channel);
            }
        }
    }
}
