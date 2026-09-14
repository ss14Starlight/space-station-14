using Content.Server.Atmos.EntitySystems;
using Content.Shared.IgnitionSource;

namespace Content.Server.IgnitionSource;

public sealed partial class IgnitionSourceSystem : SharedIgnitionSourceSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    #region Starlight
    private float _updateAccumulator;
    private const float UpdateInterval = 0.25f;
    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        #region Starlight
        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        _updateAccumulator -= UpdateInterval;
        #endregion
        // Starlight - most ignition sources (every welder, lighter, anything that burned once) are off,
        // only fetch the transform for lit ones.
        var query = EntityQueryEnumerator<IgnitionSourceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Ignited)
                continue;

            var xform = Transform(uid); // Starlight-edit
            if (xform.GridUid is { } gridUid)
            {
                var position = _transform.GetGridOrMapTilePosition(uid, xform);
                // TODO: Should this be happening every single tick?
                _atmosphere.HotspotExpose(gridUid, position, comp.Temperature, 50, uid, true);
            }
        }
    }
}
