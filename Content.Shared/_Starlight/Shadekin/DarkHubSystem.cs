using Content.Shared.Popups;
using Content.Shared.Teleportation.Components;
using Content.Shared.Warps;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Shadekin;

public sealed class DarkHubSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkHubComponent, OnAttemptPortalEvent>(OnAttemptPortal);
    }
    // I only one have thing to say... WHY DOES THIS WORK WHEN DARK PORTAL DOES NOT? YOU ARE BULLSHIT GAME! Thank you peoples
    // But yes, this one is shared and its works... THE OTHERS DOES NOT AND I HAVE TO MAKE COPY AND PASTE FOR PREDICTION... FUCK YOU!
    // I suffer by watching this.

    // Note: I actully i am unsure if client is even fired on this... How? Why? right now its fucking works but... I AM CONFUSED!
    // WHAT MAGIC IS THIS?

    private void OnAttemptPortal(EntityUid uid, DarkHubComponent component, OnAttemptPortalEvent args)
    {
        if (TryComp<BrighteyeComponent>(args.Subject, out var brighteye) && brighteye.Rejuvenating)
        {
            _popup.PopupClient(Loc.GetString("hubportal-rejuvenate"), args.Subject, args.Subject, PopupType.LargeCaution);
            args.Cancel();
            return;
        }

        if (_netMan.IsClient) // Predict Randomness? NO THANK YOU!
        {
            args.Cancel();
            return;
        }

        if (TryComp<LinkedEntityComponent>(uid, out var link))
        {
            if (link.LinkedEntities.Count != 0)
                return;
        }

        // No Links? No Portals? Lets return to a random safe warp point on station!

        HashSet<EntityUid> warps = new();

        var query = EntityQueryEnumerator<WarpPointComponent>();
        while (query.MoveNext(out var warpEnt, out var warpPointComp))
        {
            if (_whitelist.IsWhitelistPass(warpPointComp.Blacklist, warpEnt) || string.IsNullOrWhiteSpace(warpPointComp.Location))
                continue;

            warps.Add(warpEnt);
        }

        var target = _random.Pick(warps);

        var coords = Transform(target).Coordinates;
        SpawnAtPosition(component.ShadekinShadow, coords);
        _transform.SetCoordinates(args.Subject, coords);


        args.Cancel(); // Duh, we need to handle the teleport ourself!
    }
}