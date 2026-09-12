using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Chunking;
using Content.Shared.GameTicking;
using Content.Shared.Rounding;
using JetBrains.Annotations;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable once RedundantUsingDirective

namespace Content.Server.Atmos.EntitySystems;

[UsedImplicitly]
public sealed partial class GasTileOverlaySystem : SharedGasTileOverlaySystem
{
    [Robust.Shared.IoC.Dependency] private IGameTiming _gameTiming = default!;
    [Robust.Shared.IoC.Dependency] private IPlayerManager _playerManager = default!;
    [Robust.Shared.IoC.Dependency] private SharedMapSystem _mapManager = default!;
    [Robust.Shared.IoC.Dependency] private IParallelManager _parMan = default!;
    [Robust.Shared.IoC.Dependency] private AtmosphereSystem _atmosphereSystem = default!;
    [Robust.Shared.IoC.Dependency] private ChunkingSystem _chunkingSys = default!;

    /// <summary>
    /// Per-tick cache of sessions.
    /// </summary>
    private readonly List<ICommonSession> _sessions = new();
    private UpdatePlayerJob _updateJob;
    private UpdateChunksJob _updateChunksJob;

    #region Starlight

    private readonly List<(GasOverlayChunk Chunk, Vector2i Tile)> _tileUpdates = [];
    private readonly List<GasOverlayChunk> _chunkUpdates = [];

    private const int ParallelTileThreshold = 64;

    private readonly Dictionary<ICommonSession, Dictionary<NetEntity, HashSet<Vector2i>>> _lastSentChunks = [];

    #endregion

    // Oh look its more duplicated decal system code!
    private readonly ObjectPool<HashSet<Vector2i>> _chunkIndexPool =
        new DefaultObjectPool<HashSet<Vector2i>>(
            new DefaultPooledObjectPolicy<HashSet<Vector2i>>(), 64);
    private readonly ObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>> _chunkViewerPool =
        new DefaultObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>>(
            new DefaultPooledObjectPolicy<Dictionary<NetEntity, HashSet<Vector2i>>>(), 64);

    private bool _doSessionUpdate;

    /// <summary>
    ///     Overlay update interval, in seconds.
    /// </summary>
    private float _updateInterval;

    private int _thresholds;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<GasTileOverlayComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<GasTileOverlayComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        _updateJob = new UpdatePlayerJob()
        {
            EntManager = EntityManager,
            System = this,
            ChunkIndexPool = _chunkIndexPool,
            Sessions = _sessions,
            ChunkingSys = _chunkingSys,
            MapManager = _mapManager,
            ChunkViewerPool = _chunkViewerPool,
            LastSentChunks = _lastSentChunks,
            GridQuery = _gridQuery,
        };
        // Starlight-start

        _updateChunksJob = new UpdateChunksJob
        {
            System = this,
            Tiles = _tileUpdates,
        };

        // Starlight-end

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        InitializeCVars();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(Reset);
        SubscribeLocalEvent<GasTileOverlayComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, GasTileOverlayComponent component, ComponentStartup args)
        // This **shouldn't** be required, but just in case we ever get entity prototypes that have gas overlays, we
        // need to ensure that we send an initial full state to players.
        => Dirty(uid, component);

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPvsToggle(bool value)
    {
        if (value == PvsEnabled)
            return;

        PvsEnabled = value;

        if (value)
            return;

        foreach (var lastSent in _lastSentChunks.Values)
        {
            foreach (var set in lastSent.Values)
            {
                set.Clear();
                _chunkIndexPool.Return(set);
            }
            lastSent.Clear();
        }

        // PVS was turned off, ensure data gets sent to all clients.
        var query = AllEntityQuery<GasTileOverlayComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var grid, out var meta))
        {
            grid.ForceTick = _gameTiming.CurTick;
            Dirty(uid, grid, meta);
        }
    }

    private void UpdateTickRate(float value) => _updateInterval = value > 0.0f ? 1 / value : float.MaxValue;
    private void UpdateThresholds(int value) => _thresholds = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invalidate(Entity<GasTileOverlayComponent?> grid, Vector2i index)
    {
        if (_query.Resolve(grid.Owner, ref grid.Comp))
            grid.Comp.InvalidTiles.Add(index);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.InGame)
        {
            if (_lastSentChunks.Remove(e.Session, out var sets))
            {
                foreach (var set in sets.Values)
                {
                    set.Clear();
                    _chunkIndexPool.Return(set);
                }
            }
        }

        if (!_lastSentChunks.ContainsKey(e.Session))
        {
            _lastSentChunks[e.Session] = new();
        }
    }

    private byte GetOpacity(float moles, float molesVisible, float molesVisibleMax)
        => (byte) (ContentHelpers.RoundToLevels(MathHelper.Clamp01((moles - molesVisible) / (molesVisibleMax - molesVisible)) * 255, byte.MaxValue, _thresholds) * 255 / (_thresholds - 1));

    public GasOverlayData GetOverlayData(GasMixture? mixture)
    {
        ThermalByte byteTemp;
        if (mixture == null)
        {
            byteTemp = new();
            byteTemp.SetVacuum();
        }
        else
            byteTemp = new(mixture.Temperature);

        var opacity = new GasOpacityData(); // Starlight-edit

        for (var i = 0; i < VisibleGasId.Length; i++)
        {
            var id = VisibleGasId[i];
            var gas = _atmosphereSystem.GetGas(id);
            var moles = mixture?[id] ?? 0f;

            if (moles < gas.GasMolesVisible)
                continue;

            opacity[i] = GetOpacity(moles, gas.GasMolesVisible, gas.GasMolesVisibleMax); // Starlight-edit
        }

        return new GasOverlayData(0, opacity, byteTemp); // Starlight-edit
    }

    /// <summary>
    ///     Updates the visuals for a tile on some grid chunk. Returns true if the visuals have changed.
    /// </summary>
    private void UpdateChunkTile(GridAtmosphereComponent gridAtmosphere, GasOverlayChunk chunk, Vector2i index, GameTick curTick) // Starlight-edit
    {
        // Starlight-start
        var dataIndex = chunk.GetDataIndex(index);
        ref var oldData = ref chunk.TileData[dataIndex];
        // Starlight-end

        if (!gridAtmosphere.Tiles.TryGetValue(index, out var tile))
        {
            if (oldData.Equals(default))
                return; // Starlight-edit

            oldData = default;
            MarkTileDirty(chunk, dataIndex, curTick); // Starlight-edit
            return; // Starlight-edit
        }

        ThermalByte newByteTemp = new();

        if (tile.Hotspot.Valid)
            newByteTemp.SetTemperature(tile.Hotspot.Temperature);
        else if (!tile.Space && tile.Air?.TotalMoles <= 5f)
            newByteTemp.SetVacuum();
        else if (!tile.Space && tile.Air != null)
            newByteTemp = new(tile.Air.Temperature);

        // Starlight-start

        var changed = oldData.Equals(default)
            || oldData.FireState != tile.Hotspot.State
            || Math.Abs(oldData.ByteGasTemperature.Value - newByteTemp.Value) > 1
            || (oldData.ByteGasTemperature.Value != newByteTemp.Value && newByteTemp.Value > ThermalByte.TempResolution);

        var temperature = changed ? newByteTemp : oldData.ByteGasTemperature;
        var opacity = oldData.Opacity;

        // Starlight-end

        if (tile is {Air: not null, NoGridTile: false})
        {
            for (var i = 0; i < VisibleGasId.Length; i++)
            {
                var id = VisibleGasId[i];
                var gas = _atmosphereSystem.GetGas(id);
                var moles = tile.Air[id];

                // Starlight-start
                var newOpacity = moles < gas.GasMolesVisible
                    ? (byte) 0
                    : GetOpacity(moles, gas.GasMolesVisible, gas.GasMolesVisibleMax);

                if (opacity[i] == newOpacity)
                    continue;

                opacity[i] = newOpacity;
                // Starlight-end
                changed = true;
            }
        }
        // Starlight-start
        else if (!opacity.IsEmpty)
        {
            opacity = default;
            changed = true;
        }

        if (!changed)
            return;

        oldData = new GasOverlayData(tile.Hotspot.State, opacity, temperature);
        MarkTileDirty(chunk, dataIndex, curTick);
        // Starlight-end
    }

    #region Starlight

    private static void MarkTileDirty(GasOverlayChunk chunk, int dataIndex, GameTick curTick)
    {
        chunk.LastUpdate = curTick;
        // Tiles of the same chunk can be updated by different threads.
        Interlocked.Or(ref chunk.DirtyTiles, 1ul << dataIndex);
    }

    private void UpdateOverlayData()
    {
        var curTick = _gameTiming.CurTick;

        var query = AllEntityQuery<GasTileOverlayComponent, GridAtmosphereComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var overlay, out var gam, out var meta))
        {
            if (overlay.InvalidTiles.Count == 0)
                continue;

            // Resolving which chunk every invalidated tile belongs to happens before the parallel pass,
            // since it can create new chunks.
            _tileUpdates.Clear();
            _chunkUpdates.Clear();

            foreach (var index in overlay.InvalidTiles)
            {
                var chunkIndex = GetGasChunkIndices(index);

                if (!overlay.Chunks.TryGetValue(chunkIndex, out var chunk))
                    overlay.Chunks[chunkIndex] = chunk = new GasOverlayChunk(chunkIndex);

                if (!chunk.UpdateQueued)
                {
                    chunk.UpdateQueued = true;
                    // Whatever changed in the previous update has been sent out by now.
                    chunk.DirtyTiles = 0;
                    chunk.Delta = null;
                    _chunkUpdates.Add(chunk);
                }

                _tileUpdates.Add((chunk, index));
            }

            overlay.InvalidTiles.Clear();

            try
            {
                // Only worth spreading over threads once there is a decent amount of tiles to go through.
                if (_tileUpdates.Count >= ParallelTileThreshold)
                {
                    _updateChunksJob.Atmosphere = gam;
                    _updateChunksJob.CurTick = curTick;
                    _parMan.ProcessNow(_updateChunksJob, _tileUpdates.Count);
                }
                else
                {
                    foreach (var (chunk, tile) in _tileUpdates)
                    {
                        UpdateChunkTile(gam, chunk, tile, curTick);
                    }
                }
            }
            finally
            {
                foreach (var chunk in _chunkUpdates)
                {
                    chunk.UpdateQueued = false;
                }
            }

            var changed = false;

            foreach (var chunk in _chunkUpdates)
            {
                if (chunk.LastUpdate != curTick)
                    continue;

                changed = true;
                BuildChunkDelta(chunk);
            }

            if (changed)
                Dirty(uid, overlay, meta);
        }
    }

    /// <summary>
    ///     Collects the tiles that changed this update, so players that already have the chunk only get those.
    /// </summary>
    private static void BuildChunkDelta(GasOverlayChunk chunk)
    {
        var count = BitOperations.PopCount(chunk.DirtyTiles);

        // Past a point, sending the whole chunk is cheaper than describing which tiles changed.
        if (count is 0 or > (ChunkSize * ChunkSize / 2))
            return;

        var delta = new GasOverlayChunkDelta
        {
            Index = chunk.Index,
            Tiles = chunk.DirtyTiles,
            Data = new GasOverlayData[count],
        };

        var tiles = chunk.DirtyTiles;
        var i = 0;
        while (tiles != 0)
        {
            var dataIndex = BitOperations.TrailingZeroCount(tiles);
            tiles &= tiles - 1;
            delta.Data[i++] = chunk.TileData[dataIndex];
        }

        chunk.Delta = delta;
    }

    private record struct UpdateChunksJob : IParallelRobustJob
    {
        public int BatchSize => 16;

        public GasTileOverlaySystem System;
        public List<(GasOverlayChunk Chunk, Vector2i Tile)> Tiles;
        public GridAtmosphereComponent Atmosphere;
        public GameTick CurTick;

        public void Execute(int index)
        {
            var (chunk, tile) = Tiles[index];
            System.UpdateChunkTile(Atmosphere, chunk, tile, CurTick);
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        AccumulatedFrameTime += frameTime;

        if (_doSessionUpdate)
        {
            UpdateSessions();
            return;
        }

        if (AccumulatedFrameTime < _updateInterval)
            return;

        AccumulatedFrameTime -= _updateInterval;

        // First, update per-chunk visual data for any invalidated tiles.
        UpdateOverlayData();

        // Then, next tick we send the data to players.
        // This is to avoid doing all the work in the same tick.
        _doSessionUpdate = true;
    }

    public void UpdateSessions()
    {
        _doSessionUpdate = false;

        if (!PvsEnabled)
            return;

        // Now we'll go through each player, then through each chunk in range of that player checking if the player is still in range
        // If they are, check if they need the new data to send (i.e. if there's an overlay for the gas).
        // Afterwards we reset all the chunk data for the next time we tick.
        _sessions.Clear();

        foreach (var player in _playerManager.Sessions)
        {
            if (player.Status != SessionStatus.InGame)
                continue;

            _sessions.Add(player);
        }

        if (_sessions.Count == 0)
            return;

        _parMan.ProcessNow(_updateJob, _sessions.Count);
        _updateJob.LastSessionUpdate = _gameTiming.CurTick;
    }

    public void Reset(RoundRestartCleanupEvent ev)
    {
        foreach (var data in _lastSentChunks.Values)
        {
            foreach (var previous in data.Values)
            {
                previous.Clear();
                _chunkIndexPool.Return(previous);
            }

            data.Clear();
        }
    }

    #region Jobs

    /// <summary>
    /// Updates per player gas overlay data.
    /// </summary>
    private record struct UpdatePlayerJob : IParallelRobustJob
    {
        public int BatchSize => 2;

        public IEntityManager EntManager;
        public SharedMapSystem MapManager;
        public ChunkingSystem ChunkingSys;
        public GasTileOverlaySystem System;
        public ObjectPool<HashSet<Vector2i>> ChunkIndexPool;
        public ObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>> ChunkViewerPool;

        public GameTick LastSessionUpdate;
        public Dictionary<ICommonSession, Dictionary<NetEntity, HashSet<Vector2i>>> LastSentChunks;
        public List<ICommonSession> Sessions;

        public EntityQuery<MapGridComponent> GridQuery;

        public void Execute(int index)
        {
            var playerSession = Sessions[index];
            var chunksInRange = ChunkingSys.GetChunksForSession(playerSession, ChunkSize, ChunkIndexPool, ChunkViewerPool);
            var previouslySent = LastSentChunks[playerSession];

            var ev = new GasOverlayUpdateEvent();

            foreach (var (netGrid, oldIndices) in previouslySent)
            {
                // Mark the whole grid as stale and flag for removal.
                if (!chunksInRange.TryGetValue(netGrid, out var chunks))
                {
                    previouslySent.Remove(netGrid);

                    // If grid was deleted then don't worry about sending it to the client.
                    if (!EntManager.TryGetEntity(netGrid, out var gridId) || GridQuery.HasComp(gridId.Value))
                        ev.RemovedChunks[netGrid] = oldIndices;
                    else
                    {
                        oldIndices.Clear();
                        ChunkIndexPool.Return(oldIndices);
                    }

                    continue;
                }

                var old = ChunkIndexPool.Get();
                DebugTools.Assert(old.Count == 0);
                foreach (var chunk in oldIndices)
                {
                    if (!chunks.Contains(chunk))
                        old.Add(chunk);
                }

                if (old.Count == 0)
                    ChunkIndexPool.Return(old);
                else
                    ev.RemovedChunks.Add(netGrid, old);
            }

            foreach (var (netGrid, gridChunks) in chunksInRange)
            {
                // Not all grids have atmospheres.
                if (!EntManager.TryGetEntity(netGrid, out var grid) || !EntManager.TryGetComponent(grid, out GasTileOverlayComponent? overlay))
                    continue;

                // Starlight - only allocate & send grids that actually have new chunk data,
                // otherwise every player gets an (empty) update every overlay tick.
                List<GasOverlayChunk>? dataToSend = null;
                List<GasOverlayChunkDelta>? deltasToSend = null;

                previouslySent.TryGetValue(netGrid, out var previousChunks);

                foreach (var gIndex in gridChunks)
                {
                    if (!overlay.Chunks.TryGetValue(gIndex, out var value))
                        continue;

                    var known = previousChunks != null && previousChunks.Contains(gIndex); // Starlight-edit

                    // If the chunk was updated since we last sent it, send it again
                    if (value.LastUpdate > LastSessionUpdate)
                    {
                        // Starlight-start
                        if (known && value.Delta is { } delta)
                            (deltasToSend ??= []).Add(delta);
                        else
                            (dataToSend ??= []).Add(value);
                        // Starlight-end

                        continue;
                    }

                    // Always send it if we didn't previously send it
                    // Starlight-start
                    if (!known)
                        (dataToSend ??= []).Add(value);
                    // Starlight-end
                }

                // Starlight-start
                if (dataToSend != null)
                    ev.UpdatedChunks[netGrid] = dataToSend;

                if (deltasToSend != null)
                    ev.DeltaChunks[netGrid] = deltasToSend;
                // Starlight-end

                previouslySent[netGrid] = gridChunks;
                if (previousChunks != null)
                {
                    previousChunks.Clear();
                    ChunkIndexPool.Return(previousChunks);
                }
            }

            // Starlight-start: the per-grid sets now live in previouslySent (or were dropped), reuse the dictionary.
            chunksInRange.Clear();
            ChunkViewerPool.Return(chunksInRange);

            if (ev.UpdatedChunks.Count != 0 || ev.DeltaChunks.Count != 0 || ev.RemovedChunks.Count != 0)
                System.RaiseNetworkEvent(ev, playerSession.Channel);
            // Starlight-end
        }
    }

    #endregion

    private void InitializeCVars()
    {
        Subs.CVar(ConfMan, CCVars.NetGasOverlayTickRate, UpdateTickRate, true);
        Subs.CVar(ConfMan, CCVars.GasOverlayThresholds, UpdateThresholds, true);
        Subs.CVar(ConfMan, CVars.NetPVS, OnPvsToggle, true);
    }
}
