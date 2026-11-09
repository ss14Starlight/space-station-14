// Begin Starlight (DevoutChap)
using Content.Server.Atmos.EntitySystems;
using Content.Server.Prayer.Components;
using Content.Shared.Atmos.Components;

namespace Content.Server.Prayer;

public sealed partial class IgniteUserOnPraySystem : EntitySystem
{
    [Dependency] private FlammableSystem _flammable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IgniteUserOnPrayComponent, PrayerPerformedEvent>(OnPrayerPerformed);
    }

    private void OnPrayerPerformed(Entity<IgniteUserOnPrayComponent> ent, ref PrayerPerformedEvent args)
    {
        if (!TryComp<FlammableComponent>(args.User, out var flammable))
            return;

        _flammable.AdjustFireStacks(args.User, 1f, flammable, ignite: true);
    }
}
// End Starlight
