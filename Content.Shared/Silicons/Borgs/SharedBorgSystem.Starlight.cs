using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Shared.Silicons.Borgs;

public abstract partial class SharedBorgSystem
{
    [SubscribeLocalEvent]
    private void OnPlayerDetached(Entity<BorgChassisComponent> chassis, ref PlayerDetachedEvent args)
        => SetActive(chassis, false);
}
