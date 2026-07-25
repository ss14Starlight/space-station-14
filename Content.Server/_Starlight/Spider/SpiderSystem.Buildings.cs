using Content.Server.Popups;
using Content.Shared.Spider;
using Content.Shared._Starlight.Spider.Events;

namespace Content.Server._Starlight.Spider;

public sealed partial class SpiderBuildingsSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpiderComponent, SpiderWebBuildingActionEvent>(OnSpawnBuilding);
    }

    private void OnSpawnBuilding(EntityUid uid, SpiderComponent component, SpiderWebBuildingActionEvent args)
    {
        if (args.Handled)
            return;

        var transform = Transform(uid);

        if (transform.GridUid == null)
        {
            _popup.PopupEntity(Loc.GetString("spider-web-action-nogrid"), args.Performer, args.Performer);
            return;
        }

        Spawn(args.Building, transform.Coordinates);

        _popup.PopupEntity(Loc.GetString("spider-web-action-success"), args.Performer, args.Performer);
        args.Handled = true;
    }
}
