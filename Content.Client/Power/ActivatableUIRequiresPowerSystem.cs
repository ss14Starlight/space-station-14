using Content.Client.Power.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;

namespace Content.Client.Power;

public sealed partial class ActivatableUIRequiresPowerSystem : SharedActivatableUIRequiresPowerSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    protected override void OnActivate(Entity<ActivatableUIRequiresPowerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled ||
            HasPowerCellFallback(ent.Owner, ent.Comp) || // Starlight
            this.IsPowered(ent.Owner, EntityManager))
        {
            return;
        }

        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("base-computer-ui-component-not-powered", ("machine", ent.Owner)), args.User, args.User);

        args.Cancel();
    }

    private partial bool HasPowerCellFallback(EntityUid uid, ActivatableUIRequiresPowerComponent component); // Starlight
}
