using System.Linq;
using Content.Client.Alerts;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Body.Systems;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Dolls;

public sealed partial class ShellSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShellComponent, UpdateAlertSpriteEvent>(OnUpdateAlert);
    }

    private void OnUpdateAlert(Entity<ShellComponent> ent, ref UpdateAlertSpriteEvent args)
    {
        if (args.Alert.ID != ent.Comp.ShellAlert)
            return;

        var shellPieceAmount = Math.Clamp(_body.GetBodyOrgans(ent.Owner).Where(o => TryComp(o.Id, out OrganShellComponent? _)).Count(), 0, 10);
        _sprite.LayerSetRsiState(args.SpriteViewEnt.Owner, AlertVisualLayers.Base, $"base{shellPieceAmount/2}");
    }
}
