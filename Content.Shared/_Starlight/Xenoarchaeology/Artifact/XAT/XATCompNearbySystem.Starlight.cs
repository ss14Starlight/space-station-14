// ReSharper disable CheckNamespace
using Content.Shared.Tag;
using Content.Shared.Xenoarchaeology.Artifact.XAT.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Xenoarchaeology.Artifact.XAT;

public sealed partial class XATCompNearbySystem
{
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;
        base.Update(frameTime);
    }

    private int CountMatchingEntities(XATCompNearbyComponent comp, HashSet<Entity<IComponent>> entities)
    {
        var matched = 0;
        foreach (var ent in entities)
        {
            if (comp.RequireTag is {} requireTag && !_tag.HasTag(ent.Owner, requireTag))
                continue;
            matched++;
        }
        return matched;
    }
}
