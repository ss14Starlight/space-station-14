using Content.Server._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.Popups;
using Content.Shared.Teleportation.Components;
using Content.Shared.Verbs;
using Content.Shared.Warps;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Shadekin;

public sealed class DarkHubSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DarkPortalSystem _portal = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DarkHubComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
    }

    private void OnGetInteractionVerbs(EntityUid uid, DarkHubComponent component, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!component.Hub || !args.CanAccess || !TryComp<BrighteyeComponent>(args.User, out var brighteye) || brighteye.Portal is null)
            return;

        var user = args.User;

        args.Verbs.Add(new()
        {
            Act = () =>
            {
                SpawnAtPosition(component.ShadekinShadow, Transform(brighteye.Portal.Value).Coordinates);
                QueueDel(brighteye.Portal);
                _portal.OnPortalShutdown(user, brighteye);
            },
            Text = Loc.GetString("shadekin-portal-destroy"),
        });
    }
}
