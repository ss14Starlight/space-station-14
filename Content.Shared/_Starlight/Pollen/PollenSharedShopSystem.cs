using Content.Shared._Starlight.Pollen.Components;
using Content.Shared.DoAfter;

namespace Content.Shared._Starlight.Pollen;

public sealed class PollenSharedShopSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenCollectorComponent, MakeWoodEvent>(OnMakeWood);
        SubscribeLocalEvent<PollenCollectorComponent, PollenSporeCloudEvent>(OnSporeCloud);
        SubscribeLocalEvent<PollenCollectorComponent, PollenInjectPhytovitalinEvent>(OnInjectPhytovitalin);
    }

    private void OnMakeWood(Entity<PollenCollectorComponent> ent, ref MakeWoodEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        RaiseLocalEvent(ent.Owner, new PollenMakeWoodEffectEvent());
    }

    private void OnSporeCloud(Entity<PollenCollectorComponent> ent, ref PollenSporeCloudEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        RaiseLocalEvent(ent.Owner, new PollenSporeCloudEffectEvent());
    }

    private void OnInjectPhytovitalin(Entity<PollenCollectorComponent> ent, ref PollenInjectPhytovitalinEvent args)
    {
        if (args.Handled)
            return;

        var netAction = GetNetEntity(args.Action);
        var doAfterArgs = new DoAfterArgs(EntityManager, ent.Owner, TimeSpan.FromSeconds(10),
            new PollenInjectPhytovitalinDoAfterEvent(netAction), ent.Owner, target: args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        args.Handled = true;
    }
}
