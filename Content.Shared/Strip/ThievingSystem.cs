using Content.Shared.Alert;
using Content.Shared.Inventory;
using Content.Shared.Strip.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Strip;

public sealed partial class ThievingSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    //Starlight start
    private readonly TimeSpan MaxStripReduction = TimeSpan.FromSeconds(-2); // Since we kept thieving gloves, we need to prevent insta-thieving.
    private readonly TimeSpan AdminStripReduction = TimeSpan.FromSeconds(-1000); // Admins still need to be able to instant strip. Their time is 9999, but I've kept this at 1000 incase an admin somehow gets steal time reduced to 9998.
    //Starlight end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThievingComponent, BeforeStripEvent>(OnBeforeStrip);
        SubscribeLocalEvent<ThievingComponent, InventoryRelayedEvent<BeforeStripEvent>>((e, c, ev) =>
            OnBeforeStrip(e, c, ev.Args));
        SubscribeLocalEvent<ThievingComponent, ToggleThievingEvent>(OnToggleStealthy);
        SubscribeLocalEvent<ThievingComponent, ComponentInit>(OnCompInit);
        SubscribeLocalEvent<ThievingComponent, ComponentRemove>(OnCompRemoved);
    }

    private void OnBeforeStrip(EntityUid uid, ThievingComponent component, BeforeStripEvent args)
    {
        args.Stealth |= component.Stealthy;
        if (args.Stealth)
        {
            args.Additive -= component.StripTimeReduction;
        }
        // Starlight start
        if (args.Additive < MaxStripReduction && args.Additive > AdminStripReduction)
        {
            args.Additive = MaxStripReduction; //We kept thieving gloves, but them combining to 3 makes it so you can instant-steal stuff, so this fixes that, without making everything take longer to steal.
        }
        // Starlight end
    }

    private void OnCompInit(Entity<ThievingComponent> entity, ref ComponentInit args)
    {
        _alertsSystem.ShowAlert(entity.Owner, entity.Comp.StealthyAlertProtoId, 1);
    }

    private void OnCompRemoved(Entity<ThievingComponent> entity, ref ComponentRemove args)
    {
        _alertsSystem.ClearAlert(entity.Owner, entity.Comp.StealthyAlertProtoId);
    }

    private void OnToggleStealthy(Entity<ThievingComponent> ent, ref ToggleThievingEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.Stealthy = !ent.Comp.Stealthy;
        _alertsSystem.ShowAlert(ent.Owner, ent.Comp.StealthyAlertProtoId, (short)(ent.Comp.Stealthy ? 1 : 0));
        DirtyField(ent.AsNullable(), nameof(ent.Comp.Stealthy), null);

        args.Handled = true;
    }
}
