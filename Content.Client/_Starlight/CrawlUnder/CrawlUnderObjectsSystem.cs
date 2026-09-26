using Content.Shared._Starlight.CrawlUnder;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.CrawlUnder;

public sealed partial class CrawlUnderObjectsSystem : SharedCrawlUnderObjectsSystem
{
    [Dependency] private IGameTiming _gameTiming = default!;

    protected override bool TryPopupCooldown(CrawlUnderObjectsComponent comp)
    {
        if (comp.LastFailedPopup + comp.FailedPopupCooldown >= _gameTiming.CurTime)
            return false;

        comp.LastFailedPopup = _gameTiming.CurTime;
        return true;
    }
}
