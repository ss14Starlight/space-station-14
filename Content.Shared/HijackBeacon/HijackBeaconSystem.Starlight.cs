using System.Linq;

namespace Content.Shared.HijackBeacon;

public sealed partial class HijackBeaconSystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;

    private bool SLCanActivate(Entity<HijackBeaconComponent> ent)
        => _lookup.GetEntitiesIntersecting(ent)
            .Any(HasComp<HijackPointComponent>);
}

[RegisterComponent]
public sealed partial class HijackPointComponent : Component;