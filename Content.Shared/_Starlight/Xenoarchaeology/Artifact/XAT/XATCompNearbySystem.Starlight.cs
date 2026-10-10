// ReSharper disable CheckNamespace
using Content.Shared.Tag;
using Content.Shared.Xenoarchaeology.Artifact.XAT.Components;

namespace Content.Shared.Xenoarchaeology.Artifact.XAT;

public sealed partial class XATCompNearbySystem
{
    [Dependency] private TagSystem _tag = default!;

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
