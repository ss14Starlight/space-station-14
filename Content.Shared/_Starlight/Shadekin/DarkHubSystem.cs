using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Popups;
using Content.Shared.Teleportation.Components;
using Content.Shared.Verbs;
using Content.Shared.Warps;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Shadekin;

public sealed partial class DarkHubSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DarkPortalSystem _portal = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkHubComponent, OnAttemptPortalEvent>(OnAttemptPortal);
        SubscribeLocalEvent<DarkHubComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
    }

    private void OnAttemptPortal(Entity<DarkHubComponent> ent, ref OnAttemptPortalEvent args)
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

        if (TryComp<LinkedEntityComponent>(ent, out var link))
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
        SpawnAtPosition(ent.Comp.ShadekinShadow, coords);
        _transform.SetCoordinates(args.Subject, coords);


        args.Cancel(); // Duh, we need to handle the teleport ourself!
    }

    private void OnGetInteractionVerbs(Entity<DarkHubComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!ent.Comp.Hub || !args.CanAccess || !TryComp<BrighteyeComponent>(args.User, out var brighteye) || brighteye.Portal is null)
            return;

        var user = args.User;

        args.Verbs.Add(new InteractionVerb
        {
            Act = () =>
            {
                PredictedSpawnAtPosition(ent.Comp.ShadekinShadow, Transform(brighteye.Portal.Value).Coordinates);
                PredictedQueueDel(brighteye.Portal);
                _portal.OnPortalShutdown((user, brighteye));
            },
            Text = Loc.GetString("shadekin-portal-destroy"),
        });
    }
}
