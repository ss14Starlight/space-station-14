using Content.Shared._Starlight.IdentityManagement.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement.Components;

namespace Content.Shared._Starlight.IdentityManagement;

public sealed partial class HeldIdentityBlockerSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HandsComponent, SeeIdentityAttemptEvent>(OnSeeIdentity);
    }

    private void OnSeeIdentity(Entity<HandsComponent> ent, ref SeeIdentityAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var uid = ent.Owner;
        var hands = ent.Comp;

        foreach (var held in _hands.EnumerateHeld((uid, hands)))
        {
            if (!TryComp<HeldIdentityBlockerComponent>(held, out var blocker) || !blocker.Enabled)
                continue;

            args.TotalCoverage |= blocker.Coverage;
            if (args.TotalCoverage == IdentityBlockerCoverage.FULL)
            {
                args.Cancel();
                return;
            }
        }
    }
}
