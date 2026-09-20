using Content.Server.Atmos.EntitySystems;
using Content.Shared.IgnitionSource;

namespace Content.Server.IgnitionSource;

public sealed partial class IgnitionSourceSystem : SharedIgnitionSourceSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        #region Starlight
        // pretty much redid all of this
        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        _updateAccumulator -= UpdateInterval;

        if (_activeSources.Count == 0)
            return;

        // Snapshot because exposing a tile can ignite or extinguish entities and mutate the active set.
        _sourceSnapshot.Clear();
        _sourceSnapshot.AddRange(_activeSources);
        _tileExposures.Clear();

        foreach (var uid in _sourceSnapshot)
        {
            if (!_ignitionQuery.TryComp(uid, out var comp) || !comp.Ignited)
            {
                _activeSources.Remove(uid);
                continue;
            }

            if (!_transformQuery.TryComp(uid, out var xform) || xform.GridUid is not { } gridUid)
                continue;

            var position = _transform.GetGridOrMapTilePosition(uid, xform);
            var key = (gridUid, position);

            // Multiple flames on one tile sustain the same hotspot. Only the hottest exposure matters.
            if (!_tileExposures.TryGetValue(key, out var exposure) || comp.Temperature > exposure.Temperature)
                _tileExposures[key] = new IgnitionExposure(comp.Temperature, uid);
        }

        foreach (var (key, exposure) in _tileExposures)
        {
            _atmosphere.HotspotExpose(key.Grid, key.Tile, exposure.Temperature, 50f, exposure.Source, true);
        }
        #endregion
    }
}
