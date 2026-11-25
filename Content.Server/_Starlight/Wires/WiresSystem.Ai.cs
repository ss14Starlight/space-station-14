using Content.Shared.Hands.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Wires;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tools.Components;
using Content.Shared.Wires;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

// ReSharper disable CheckNamespace
namespace Content.Server.Wires;

public sealed partial class WiresSystem
{
    private static readonly TimeSpan StationAiWireCutHoldDuration = TimeSpan.FromSeconds(1);

    [Dependency] private SharedRemoteControlConsoleSystem _sharedRemoteControl = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = null!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, (EntityUid Target, int WireId, TimeSpan StartedAt)> _stationAiWireCutHolds = new();

    [SubscribeLocalEvent]
    private void OnStationAiWireCutHoldMessage(EntityUid target, WiresComponent wires, StationAiWireCutHoldMessage args)
    {
        if (!args.Started)
        {
            if (_stationAiWireCutHolds.TryGetValue(args.Actor, out var activeHold)
                && activeHold.Target == target
                && activeHold.WireId == args.Id)
            {
                _stationAiWireCutHolds.Remove(args.Actor);
            }

            return;
        }

        _stationAiWireCutHolds.Remove(args.Actor);

        if (!HasComp<StationAiHeldComponent>(args.Actor)
            || !TryComp<StationAiWhitelistComponent>(target, out var whitelist)
            || !whitelist.Enabled
            || !HasComp<WiresPanelComponent>(target)
            || !_uiSystem.IsUiOpen(target, WiresUiKey.Key, args.Actor)
            || (TryComp<WiresPanelSecurityComponent>(target, out var panelSecurity)
                && !panelSecurity.WiresAccessible))
        {
            return;
        }

        if (TryGetWire(target, args.Id, wires) == null)
            return;

        _stationAiWireCutHolds[args.Actor] = (target, args.Id, _timing.CurTime);
    }

    [SubscribeLocalEvent]
    private void OnStationAiOpenWiresPanel(EntityUid uid, WiresComponent component, StationAiOpenWiresPanelEvent args)
    {
        if (!HasComp<StationAiHeldComponent>(args.User)
            || !TryComp<StationAiWhitelistComponent>(uid, out var whitelist)
            || !whitelist.Enabled
            || !HasComp<WiresPanelComponent>(uid)
            || !_uiSystem.HasUi(uid, WiresUiKey.Key)
            || (TryComp<WiresPanelSecurityComponent>(uid, out var panelSecurity)
                && !panelSecurity.WiresAccessible))
        {
            return;
        }

        component.ViewWires = false;
        UpdateUserInterface(uid, component);
        _uiSystem.OpenUi(uid, WiresUiKey.Key, args.User);
    }

    [SubscribeLocalEvent]
    private void OnStationAiWiresPanelMessageAttempt(EntityUid uid, WiresComponent component, ref BoundUserInterfaceMessageAttempt args)
    {
        if (!args.UiKey.Equals(WiresUiKey.Key)
            || args.Message is not OpenBoundInterfaceMessage
            || !HasComp<StationAiHeldComponent>(args.Actor))
        {
            return;
        }

        if (!TryComp<StationAiWhitelistComponent>(uid, out var whitelist)
            || !whitelist.Enabled
            || !HasComp<WiresPanelComponent>(uid)
            || !_uiSystem.HasUi(uid, WiresUiKey.Key)
            || (TryComp<WiresPanelSecurityComponent>(uid, out var panelSecurity)
                && !panelSecurity.WiresAccessible))
        {
            args.Cancel();
            return;
        }

        component.ViewWires = false;
        UpdateUserInterface(uid, component);
    }

    private bool TryDoStationAiWireAction(EntityUid target, EntityUid user, int id, WiresAction action, WiresComponent wires)
    {
        if (!HasComp<StationAiHeldComponent>(user))
            return false;

        if (action == WiresAction.Cut && !TryConsumeStationAiWireCutHold(target, user, id))
            return true;

        if ((action != WiresAction.Cut && action != WiresAction.Pulse)
            || !TryComp<StationAiWhitelistComponent>(target, out var whitelist)
            || !whitelist.Enabled
            || !HasComp<WiresPanelComponent>(target)
            || !_uiSystem.IsUiOpen(target, WiresUiKey.Key, user)
            || (TryComp<WiresPanelSecurityComponent>(target, out var panelSecurity)
                && !panelSecurity.WiresAccessible)
            || wires.WiresQueue.Contains(id))
        {
            return true;
        }

        var wire = TryGetWire(target, id, wires);
        if (wire == null)
            return true;

        if (action == WiresAction.Cut && !_powerReceiver.IsPowered(target))
        {
            _popupSystem.PopupCursor(Loc.GetString("ai-device-not-responding"), user);
            return true;
        }

        if (wire.IsCut)
        {
            var message = action == WiresAction.Cut
                ? "wires-component-ui-on-receive-message-cannot-cut-cut-wire"
                : "wires-component-ui-on-receive-message-cannot-pulse-cut-wire";
            _popupSystem.PopupCursor(Loc.GetString(message), user);
            return true;
        }

        wires.WiresQueue.Add(id);

        if (action == WiresAction.Cut)
        {
            if (wire.Action == null || wire.Action.Cut(user, wire))
            {
                wire.IsCut = true;
                Audio.PlayPvs(new SoundCollectionSpecifier("sparks"), target);
            }

            UpdateUserInterface(target, wires);
        }
        else
        {
            wire.Action?.Pulse(user, wire);
            UpdateUserInterface(target, wires);
            Audio.PlayPvs(wires.PulseSound, target);
        }

        wire.Action?.Update(wire);

        if (!whitelist.Enabled)
            _uiSystem.CloseUi(target, WiresUiKey.Key, user);

        wires.WiresQueue.Remove(id);
        return true;
    }

    private bool TryConsumeStationAiWireCutHold(EntityUid target, EntityUid user, int id)
    {
        if (!_stationAiWireCutHolds.TryGetValue(user, out var activeHold))
            return false;

        _stationAiWireCutHolds.Remove(user);
        return activeHold.Target == target
                && activeHold.WireId == id
                && _timing.CurTime - activeHold.StartedAt >= StationAiWireCutHoldDuration;
    }

    /// <summary>
    /// Attempts to open the wires user interface for a remote user.
    /// </summary>
    /// <returns>True if the interface was opened.</returns>
    public bool TryOpenRemoteUserInterface(
        Entity<WiresComponent?> target,
        EntityUid remoteUser,
        EntityUid used)
    {
        if (!Resolve(target.Owner, ref target.Comp))
            return false;

        if (!_sharedRemoteControl.TryGetControllerForRemoteEntity(remoteUser, out _))
            return false;

        target.Comp.ViewWires = _tags.HasTag(used, target.Comp.ShowWiresTag);
        UpdateUserInterface(target.Owner, target.Comp);
        _uiSystem.OpenUi(target.Owner, WiresUiKey.Key, remoteUser);
        return true;
    }

    /// <summary>
    /// Attempts to perform a wire action initiated by a remote user.
    /// </summary>
    public void TryDoRemoteWireAction(
        Entity<WiresComponent?> target,
        EntityUid remoteUser,
        WiresActionMessage message)
    {
        if (!Resolve(target.Owner, ref target.Comp))
            return;

        if (!TryComp(remoteUser, out HandsComponent? hands))
        {
            _popupSystem.PopupEntity(Loc.GetString("wires-component-ui-on-receive-message-no-hands"), target.Owner, remoteUser);
            return;
        }

        if (!_interactionSystem.InRangeUnobstructed(remoteUser, target.Owner))
        {
            _popupSystem.PopupEntity(Loc.GetString("wires-component-ui-on-receive-message-cannot-reach"), target.Owner, remoteUser);
            return;
        }

        if (!_hands.TryGetActiveItem((remoteUser, hands), out var heldEntity)
            || !TryComp(heldEntity, out ToolComponent? tool))
            return;

        TryDoWireAction(target.Owner, remoteUser, heldEntity.Value, message.Id, message.Action, target.Comp, tool);
    }
}
