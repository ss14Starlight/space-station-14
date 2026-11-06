// ReSharper disable CheckNamespace
using Content.Shared.Tag;
using Content.Shared.Xenoarchaeology.Artifact.XAT.Components;

namespace Content.Shared.Xenoarchaeology.Artifact.XAT;

public sealed partial class XATCompNearbySystem
{
    [Dependency] private TagSystem _tag = default!;

    public override void Update(float frameTime)
    {
        if (Timing.CurTick.Value % (uint) Timing.TickRate != 0)
            return;

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
