using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Shared._Sol.Shuttles;
using Content.Shared._Sol.Shuttles.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Sol.Shuttles;

/// <summary>
/// Links wallmount terminals to station anchors and proxies the anchor PowerCharge UI locally.
/// </summary>
public sealed class StationAnchorTerminalSystem : EntitySystem
{
    private const float FlashPeriod = 0.5f;

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly PowerChargeSystem _powerCharge = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAnchorTerminalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StationAnchorTerminalComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<StationAnchorTerminalComponent, LinkAttemptEvent>(OnLinkAttempt);
        SubscribeLocalEvent<StationAnchorTerminalComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<StationAnchorTerminalComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<StationAnchorTerminalComponent, AfterActivatableUIOpenEvent>(OnAfterUiOpened);
        SubscribeLocalEvent<StationAnchorTerminalComponent, SwitchChargingMachineMessage>(OnSwitch);
    }

    private void OnMapInit(Entity<StationAnchorTerminalComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return;

        var linkedEntities = _deviceLink.GetLinkedSinks((ent.Owner, source), ent.Comp.LinkingPort);
        foreach (var sink in linkedEntities)
        {
            if (!HasComp<StationAnchorComponent>(sink))
                continue;

            ent.Comp.LinkedAnchor = sink;
            Dirty(ent);
            break;
        }
    }

    private void OnNewLink(Entity<StationAnchorTerminalComponent> ent, ref NewLinkEvent args)
    {
        if (args.SourcePort != ent.Comp.LinkingPort || !HasComp<StationAnchorComponent>(args.Sink))
            return;

        ent.Comp.LinkedAnchor = args.Sink;
        Dirty(ent);
        SyncTerminalUi(ent);
    }

    private void OnLinkAttempt(Entity<StationAnchorTerminalComponent> ent, ref LinkAttemptEvent args)
    {
        if (ent.Comp.LinkedAnchor != null)
            args.Cancel();
    }

    private void OnPortDisconnected(Entity<StationAnchorTerminalComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != ent.Comp.LinkingPort || ent.Comp.LinkedAnchor == null)
            return;

        ent.Comp.LinkedAnchor = null;
        Dirty(ent);
        ClearLights(ent);
        SyncTerminalUi(ent);
    }

    private void OnUiOpenAttempt(Entity<StationAnchorTerminalComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // Terminal itself must be powered (ActivatableUIRequiresPower also enforces this).
        if (!_power.IsPowered(ent.Owner))
        {
            args.Cancel();
            if (!args.Silent)
            {
                _popup.PopupEntity(
                    Loc.GetString("base-computer-ui-component-not-powered", ("machine", ent.Owner)),
                    ent,
                    args.User);
            }

            return;
        }

        // Unlinked terminals still open; the UI shows an error state.
        if (!TryGetLinkedAnchor(ent, out var anchor))
            return;

        // Do not raise ActivatableUIOpenAttempt on the anchor — that would require the
        // *anchor* to be powered. Terminal power is enough; unpowered anchors still show status.
        if (TryComp<PowerChargeComponent>(anchor, out var charge) && !charge.Intact)
            args.Cancel();
    }

    private void OnAfterUiOpened(Entity<StationAnchorTerminalComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        SyncTerminalUi(ent);
    }

    private void OnSwitch(Entity<StationAnchorTerminalComponent> ent, ref SwitchChargingMachineMessage args)
    {
        if (!_power.IsPowered(ent.Owner))
            return;

        if (!TryGetLinkedAnchor(ent, out var anchor))
            return;

        _powerCharge.SetSwitchedOn(anchor, args.On, args.Actor);
        SyncTerminalUi(ent);
    }

    private bool TryGetLinkedAnchor(
        Entity<StationAnchorTerminalComponent> ent,
        out EntityUid anchor)
    {
        anchor = default;
        if (ent.Comp.LinkedAnchor is not { } linked || TerminatingOrDeleted(linked) ||
            !HasComp<StationAnchorComponent>(linked))
            return false;

        anchor = linked;
        return true;
    }

    private void SyncTerminalUi(Entity<StationAnchorTerminalComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, PowerChargeUiKey.Key))
            return;

        if (!TryGetLinkedAnchor(ent, out var anchor))
        {
            _ui.SetUiState(ent.Owner, PowerChargeUiKey.Key,
                PowerChargeState.UnlinkedError("station-anchor-terminal-not-linked"));
            return;
        }

        _powerCharge.TrySyncUiState(anchor, ent.Owner, PowerChargeUiKey.Key);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StationAnchorTerminalComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var terminal, out var appearance))
        {
            var ent = (uid, terminal);
            UpdateLights(ent, appearance, frameTime);

            if (_ui.IsUiOpen(uid, PowerChargeUiKey.Key))
                SyncTerminalUi(ent);
        }
    }

    private void UpdateLights(Entity<StationAnchorTerminalComponent> ent, AppearanceComponent appearance, float frameTime)
    {
        if (!_power.IsPowered(ent.Owner))
        {
            SetLights(ent, appearance, broadcasting: false, speaker: false);
            return;
        }

        if (ent.Comp.LinkedAnchor is not { } anchor ||
            TerminatingOrDeleted(anchor) ||
            !TryComp<AppearanceComponent>(anchor, out var anchorAppearance))
        {
            SetLights(ent, appearance, broadcasting: false, speaker: false);
            return;
        }

        _appearance.TryGetData(anchor, PowerChargeVisuals.State, out PowerChargeStatus state, anchorAppearance);
        _appearance.TryGetData(anchor, PowerChargeVisuals.Active, out bool active, anchorAppearance);
        _appearance.TryGetData(anchor, PowerChargeVisuals.Charge, out float charge, anchorAppearance);

        switch (state)
        {
            case PowerChargeStatus.Unpowered:
            case PowerChargeStatus.Broken:
                SetLights(ent, appearance, broadcasting: true, speaker: true);
                return;
            case PowerChargeStatus.On when active:
                SetLights(ent, appearance, broadcasting: true, speaker: false);
                return;
            case PowerChargeStatus.On:
                UpdateFlash(ent, frameTime);
                SetLights(ent, appearance, broadcasting: ent.Comp.FlashLit, speaker: false);
                return;
            case PowerChargeStatus.Off when charge > 0.01f:
                UpdateFlash(ent, frameTime);
                SetLights(ent, appearance, broadcasting: false, speaker: ent.Comp.FlashLit);
                return;
            default:
                SetLights(ent, appearance, broadcasting: false, speaker: true);
                return;
        }
    }

    private void UpdateFlash(Entity<StationAnchorTerminalComponent> ent, float frameTime)
    {
        ent.Comp.FlashAccumulator += frameTime;
        if (ent.Comp.FlashAccumulator < FlashPeriod)
            return;

        ent.Comp.FlashAccumulator -= FlashPeriod;
        ent.Comp.FlashLit = !ent.Comp.FlashLit;
    }

    private void ClearLights(Entity<StationAnchorTerminalComponent> ent)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        SetLights(ent, appearance, broadcasting: false, speaker: false);
    }

    private void SetLights(EntityUid uid, AppearanceComponent appearance, bool broadcasting, bool speaker)
    {
        _appearance.SetData(uid, StationAnchorTerminalVisuals.Broadcasting, broadcasting, appearance);
        _appearance.SetData(uid, StationAnchorTerminalVisuals.Speaker, speaker, appearance);
    }
}
