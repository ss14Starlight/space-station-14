using System.Numerics;
using Content.Shared._Starlight.VentCrawl.Components;

namespace Content.Shared._Starlight.VentCrawl.EntitySystems;

public sealed partial class SharedVentCrawlSystem
{
    private void UpdateManifoldPositionInterpolated(
        EntityUid manifoldUid,
        EntityUid holderUid,
        VentCrawlHolderComponent holder)
    {
        if (holder.ManifoldLayer == null)
            return;

        var totalSeconds = (holder.ManifoldTransitionEnd - holder.ManifoldTransitionStart).TotalSeconds;

        if (totalSeconds <= 0 || holder.PreviousManifoldLayer == null)
        {
            UpdateManifoldPosition(manifoldUid, holderUid, holder);
            return;
        }

        var elapsed = (_gameTiming.CurTime - holder.ManifoldTransitionStart).TotalSeconds;
        var t = (float)Math.Clamp(elapsed / totalSeconds, 0.0, 1.0);

        if (t >= 1f)
        {
            UpdateManifoldPosition(manifoldUid, holderUid, holder);
            return;
        }

        var manifoldWorldPos = _xformSystem.GetWorldPosition(manifoldUid);
        var fromOffset = GetWorldOffsetForLayer(manifoldUid, holder.PreviousManifoldLayer.Value);
        var toOffset = GetWorldOffsetForLayer(manifoldUid, holder.ManifoldLayer.Value);

        _xformSystem.SetWorldPosition(holderUid, manifoldWorldPos + Vector2.Lerp(fromOffset, toOffset, t));
    }

    private void UpdateManifoldPosition(EntityUid manifoldUid, EntityUid holderUid, VentCrawlHolderComponent holder)
    {
        if (holder.ManifoldLayer == null)
            return;

        var worldPos = _xformSystem.GetWorldPosition(manifoldUid);
        var offset = GetWorldOffsetForLayer(manifoldUid, holder.ManifoldLayer.Value);
        _xformSystem.SetWorldPosition(holderUid, worldPos + offset);
    }
}
