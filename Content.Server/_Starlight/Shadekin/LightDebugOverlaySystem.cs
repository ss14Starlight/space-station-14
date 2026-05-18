using System.Numerics;
using Content.Shared._Starlight.Shadekin;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Shadekin;

public sealed class LightDebugOverlaySystem : SharedLightDebugOverlaySystem
{
    private const float UpdateRate = 20f;

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly LightGridSystem _lightGrid = default!;

    private readonly HashSet<ICommonSession> _playerObservers = new();
    private float _updateCooldown = 1f / UpdateRate;

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

    public bool ToggleObserver(ICommonSession observer)
    {
        if (_playerObservers.Contains(observer))
        {
            _playerObservers.Remove(observer);
            RaiseNetworkEvent(new LightDebugOverlayDisableMessage(), observer.Channel);
            return false;
        }

        _playerObservers.Add(observer);
        return true;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.InGame)
            _playerObservers.Remove(e.Session);
    }

    public override void Update(float frameTime)
    {
        AccumulatedFrameTime += frameTime;

        if (AccumulatedFrameTime < _updateCooldown)
            return;

        AccumulatedFrameTime -= _updateCooldown;

        foreach (var session in _playerObservers)
        {
            if (session.AttachedEntity is not { Valid: true } entity)
                continue;

            var xform = Transform(entity);
            var pos = _transform.GetWorldPosition(xform);
            var worldBounds = Box2.CenteredAround(pos, new Vector2(LocalViewRange, LocalViewRange));

            _grids.Clear();
            _mapManager.FindGridsIntersecting(xform.MapID, worldBounds, ref _grids);

            foreach (var grid in _grids)
            {
                if (!Exists(grid.Owner))
                    continue;

                var entityTile = _mapSystem.GetTileRef(grid, grid, xform.Coordinates).GridIndices;
                var baseTile = new Vector2i(entityTile.X - LocalViewRange / 2, entityTile.Y - LocalViewRange / 2);
                var overlayData = new byte[LocalViewRange * LocalViewRange];

                var index = 0;
                for (var y = 0; y < LocalViewRange; y++)
                {
                    for (var x = 0; x < LocalViewRange; x++)
                    {
                        var tile = new Vector2i(baseTile.X + x, baseTile.Y + y);
                        var intensity = _lightGrid.GetTileLight(grid.Owner, tile);

                        overlayData[index++] = intensity;
                    }
                }

                var msg = new LightDebugOverlayMessage(GetNetEntity(grid.Owner), baseTile, overlayData);
                RaiseNetworkEvent(msg, session.Channel);
            }
        }
    }
}
