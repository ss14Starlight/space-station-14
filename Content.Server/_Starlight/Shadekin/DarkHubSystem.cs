using Content.Shared._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Verbs;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class DarkHubSystem : EntitySystem
{
    [Dependency] private DarkPortalSystem _portal = default!;
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
