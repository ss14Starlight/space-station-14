
using Content.Shared.Radiation.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Radiation.Systems;

public sealed partial class RadiationSystem
{
    /// <summary>
    /// Normally the radiation system is supposed to track radiation sources properly, but frankly it just doesn't.
    /// This is a cheap but effective fix without us having to rework everything.
    /// </summary>
    private void UpdatePositions()
    {
        var sourceSync = EntityQueryEnumerator<RadiationSourceComponent, TransformComponent>();
        while (sourceSync.MoveNext(out var srcUid, out var srcComp, out var srcXform))
        {
            if (!srcComp.Enabled)
                continue;

            var world = _transform.GetWorldPosition(srcXform);
            if (!_sourceDataMap.TryGetValue(srcUid, out var data) || !data.WorldPosition.EqualsApprox(world))
                UpdateSource((srcUid, srcComp));
        }
    }

}
