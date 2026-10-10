using Content.Server.Power.Components;
using Content.Shared._Starlight.Power;
using Content.Shared._Starlight.Power.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server._Starlight.Power.EntitySystems;

public sealed partial class PowerCellFallbackVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnStartup(Entity<PowerCellFallbackVisualsComponent> ent, ref ComponentStartup args) =>
        UpdateBatteryAppearance(ent.Owner);

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<ApcPowerReceiverComponent> ent, ref PowerChangedEvent args) =>
        UpdateBatteryAppearance(ent.Owner);

    [SubscribeLocalEvent]
    private void OnUiOpened(Entity<PowerCellFallbackVisualsComponent> ent, ref BoundUIOpenedEvent args) =>
        UpdateBatteryAppearance(ent.Owner);

    [SubscribeLocalEvent]
    private void OnUiClosed(Entity<PowerCellFallbackVisualsComponent> ent, ref BoundUIClosedEvent args) =>
        UpdateBatteryAppearance(ent.Owner);

    [SubscribeLocalEvent]
    private void OnCellInserted(Entity<PowerCellFallbackVisualsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (TryComp<PowerCellSlotComponent>(ent, out var cellSlot) &&
            args.Container.ID == cellSlot.CellSlotId)
            UpdateBatteryAppearance(ent.Owner);
    }
    [SubscribeLocalEvent]
    private void OnCellRemoved(Entity<PowerCellFallbackVisualsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (TryComp<PowerCellSlotComponent>(ent, out var cellSlot) &&
            args.Container.ID == cellSlot.CellSlotId)
            UpdateBatteryAppearance(ent.Owner);
    }
    [SubscribeLocalEvent]
    private void OnBatteryChargeChanged(Entity<BatteryComponent> ent, ref ChargeChangedEvent args) =>
        UpdateBatteryAppearanceFromCell(ent.Owner);

    [SubscribeLocalEvent]
    private void OnBatteryStateChanged(Entity<BatteryComponent> ent, ref BatteryStateChangedEvent args) =>
        UpdateBatteryAppearanceFromCell(ent.Owner);

    private void UpdateBatteryAppearanceFromCell(EntityUid battery)
    {
        var parent = Transform(battery).ParentUid;
        if (HasComp<PowerCellFallbackVisualsComponent>(parent))
            UpdateBatteryAppearance(parent);
    }

    private void UpdateBatteryAppearance(EntityUid uid)
    {
        if (!HasComp<PowerCellFallbackVisualsComponent>(uid))
            return;

        var state = PowerCellFallbackVisualState.Off;

        if (TryComp<ActivatableUIRequiresPowerComponent>(uid, out var powerRequirement) &&
            powerRequirement.AllowPowerCellFallback &&
            HasComp<ActivatableUIRequiresPowerCellComponent>(uid) &&
            TryComp<ApcPowerReceiverComponent>(uid, out var powerReceiver) &&
            !powerReceiver.Powered &&
            _powerCell.TryGetBatteryFromSlot(uid, out var battery))
        {
            var hasCharge = _battery.GetCharge(battery.Value.AsNullable()) > 0f;
            var uiOpen = TryComp<ActivatableUIComponent>(uid, out var activatable) &&
                activatable.Key is { } key &&
                _ui.IsUiOpen(uid, key);

            if (hasCharge && uiOpen)
            {
                state = PowerCellFallbackVisualState.OnBatteryInUse;
            }
            else if (hasCharge &&
                    _powerCell.HasActivatableCharge(uid) &&
                    _powerCell.HasDrawCharge(uid))
            {
                state = PowerCellFallbackVisualState.OnBatteryIdle;
            }
        }

        _appearance.SetData(uid, PowerCellFallbackVisuals.VisualState, state);
    }
}
