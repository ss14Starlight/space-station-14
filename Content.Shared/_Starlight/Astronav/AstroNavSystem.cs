using Content.Shared.Alert;
using Content.Shared.GPS.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.GameObjects;

namespace Content.Shared._Starlight.Astronav;
public sealed partial class AstroNavSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AstroNavComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<AstroNavComponent, GotUnequippedEvent>(OnUnequip);
    }

    private void OnEquip(Entity<AstroNavComponent> ent, ref GotEquippedEvent args)
    {
        if(args.Slot != "id")
            return;
        _alerts.ShowAlert(args.EquipTarget, ent.Comp.GPSAlert);
        EnsureComp<AstroNavMobComponent>(args.EquipTarget);
        RadarConsoleComponent radarComp = EnsureComp<RadarConsoleComponent>(args.EquipTarget);
        radarComp.FollowEntity = true;
        radarComp.MaxRange = ent.Comp.MaxRange;
    }

    private void OnUnequip(Entity<AstroNavComponent> ent, ref GotUnequippedEvent args)
    {
        if(args.Slot != "id")
            return;
        _alerts.ClearAlert(args.EquipTarget, ent.Comp.GPSAlert);
        _uiSystem.CloseUi(args.EquipTarget, RadarConsoleUiKey.Key);
        // We don't remove the components since they pose no harm. The player can't access the mass scanner without the alert.
        // Removing the components also caused the client to crash, even with the UI closed.
        // Something something gamestate / component removal order. I don't know, I don't care, it works now.
    }
}
