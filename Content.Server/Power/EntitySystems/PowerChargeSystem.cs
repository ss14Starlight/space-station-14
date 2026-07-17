using Content.Server.Administration.Logs;
using Content.Server.Audio;
using Content.Server.Power.Components;
using Content.Shared.Database;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Power.EntitySystems;

public sealed partial class PowerChargeSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AmbientSoundSystem _ambientSoundSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PowerChargeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PowerChargeComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<PowerChargeComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        SubscribeLocalEvent<PowerChargeComponent, AfterActivatableUIOpenEvent>(OnAfterUiOpened);
        SubscribeLocalEvent<PowerChargeComponent, AnchorStateChangedEvent>(OnAnchorStateChange);

        // This needs to be ui key agnostic
        SubscribeLocalEvent<PowerChargeComponent, SwitchChargingMachineMessage>(OnSwitchGenerator);
    }

    private void OnAnchorStateChange(EntityUid uid, PowerChargeComponent component, AnchorStateChangedEvent args)
    {
        if (args.Anchored || !TryComp<ApcPowerReceiverComponent>(uid, out var powerReceiverComponent))
            return;

        component.Active = false;
        component.Charge = 0;
        UpdateState(new Entity<PowerChargeComponent, ApcPowerReceiverComponent>(uid, component, powerReceiverComponent));
    }

    private void OnAfterUiOpened(EntityUid uid, PowerChargeComponent component, AfterActivatableUIOpenEvent args)
    {
        if (!TryComp<ApcPowerReceiverComponent>(uid, out var apcPowerReceiver))
            return;

        UpdateUI((uid, component, apcPowerReceiver), GetChargeRate(component, apcPowerReceiver));
    }

    private void OnSwitchGenerator(EntityUid uid, PowerChargeComponent component, SwitchChargingMachineMessage args)
    {
        SetSwitchedOn(uid, component, args.On, user: args.Actor);
    }

    private void OnUIOpenAttempt(EntityUid uid, PowerChargeComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!component.Intact)
            args.Cancel();
    }

    private void OnComponentShutdown(EntityUid uid, PowerChargeComponent component, ComponentShutdown args)
    {
        if (!component.Active)
            return;

        component.Active = false;

        var eventArgs = new ChargedMachineDeactivatedEvent();
        RaiseLocalEvent(uid, ref eventArgs);
    }

    private void OnMapInit(Entity<PowerChargeComponent> ent, ref MapInitEvent args)
    {
        ApcPowerReceiverComponent? powerReceiver = null;
        if (!Resolve(ent, ref powerReceiver, false))
            return;

        UpdatePowerState(ent, powerReceiver);
        UpdateState((ent, ent.Comp, powerReceiver));
    }

    /// <summary>
    /// Sets whether the charging machine's power switch is on.
    /// </summary>
    public void SetSwitchedOn(EntityUid uid, bool on, EntityUid? user = null)
    {
        if (!TryComp<PowerChargeComponent>(uid, out var component))
            return;

        SetSwitchedOn(uid, component, on, user: user);
    }

    /// <summary>
    /// Pushes the current PowerCharge UI state from <paramref name="powerEntity"/> onto
    /// <paramref name="uiEntity"/>'s open UI. Used by remote terminals that proxy the control UI.
    /// </summary>
    public bool TrySyncUiState(EntityUid powerEntity, EntityUid uiEntity, Enum uiKey)
    {
        if (!TryComp<PowerChargeComponent>(powerEntity, out var component) ||
            !TryComp<ApcPowerReceiverComponent>(powerEntity, out var powerReceiver))
            return false;

        if (!_uiSystem.IsUiOpen(uiEntity, uiKey))
            return false;

        var chargeRate = GetChargeRate(component, powerReceiver);
        _uiSystem.SetUiState(uiEntity, uiKey, BuildUiState(component, powerReceiver, chargeRate));
        return true;
    }

    private void SetSwitchedOn(EntityUid uid, PowerChargeComponent component, bool on,
        ApcPowerReceiverComponent? powerReceiver = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref powerReceiver))
            return;

        if (user is { })
            _adminLogger.Add(LogType.Action, on ? LogImpact.Medium : LogImpact.High, $"{ToPrettyString(user):player} set {ToPrettyString(uid):target} to {(on ? "on" : "off")}");

        component.SwitchedOn = on;
        UpdatePowerState(component, powerReceiver);
        component.NeedUIUpdate = true;
    }

    private static void UpdatePowerState(PowerChargeComponent component, ApcPowerReceiverComponent powerReceiver)
    {
        powerReceiver.Load = component.SwitchedOn ? component.ActivePowerUse : component.IdlePowerUse;
    }

    private static float GetChargeRate(PowerChargeComponent chargingMachine, ApcPowerReceiverComponent powerReceiver)
    {
        // Negative charge rate means discharging.
        if (!chargingMachine.SwitchedOn)
            return -chargingMachine.ChargeRate;

        if (powerReceiver.Powered)
            return chargingMachine.ChargeRate;

        // Scale discharge rate such that if we're at 25% active power we discharge at 75% rate.
        var receiving = powerReceiver.PowerReceived;
        var mainSystemPower = Math.Max(0, receiving - chargingMachine.IdlePowerUse);
        var denom = chargingMachine.ActivePowerUse - chargingMachine.IdlePowerUse;
        var ratio = denom <= 0 ? 1 : 1 - mainSystemPower / denom;
        return -(ratio * chargingMachine.ChargeRate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PowerChargeComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var chargingMachine, out var powerReceiver))
        {
            var ent = (uid, gravGen: chargingMachine, powerReceiver);
            if (!chargingMachine.Intact)
                continue;

            var chargeRate = GetChargeRate(chargingMachine, powerReceiver);

            var active = chargingMachine.Active;
            var lastCharge = chargingMachine.Charge;
            chargingMachine.Charge = Math.Clamp(chargingMachine.Charge + frameTime * chargeRate, 0, chargingMachine.MaxCharge);
            if (chargeRate > 0)
            {
                // Charging.
                if (MathHelper.CloseTo(chargingMachine.Charge, chargingMachine.MaxCharge) && !chargingMachine.Active)
                {
                    chargingMachine.Active = true;
                }
            }
            else
            {
                // Discharging
                if (MathHelper.CloseTo(chargingMachine.Charge, 0) && chargingMachine.Active)
                {
                    chargingMachine.Active = false;
                }
            }

            var updateUI = chargingMachine.NeedUIUpdate;
            if (!MathHelper.CloseTo(lastCharge, chargingMachine.Charge))
            {
                UpdateState(ent);
                updateUI = true;
            }

            if (updateUI)
                UpdateUI(ent, chargeRate);

            if (active == chargingMachine.Active)
                continue;

            if (chargingMachine.Active)
            {
                var eventArgs = new ChargedMachineActivatedEvent();
                RaiseLocalEvent(uid, ref eventArgs);
            }
            else
            {
                var eventArgs = new ChargedMachineDeactivatedEvent();
                RaiseLocalEvent(uid, ref eventArgs);
            }
        }
    }

    private static PowerChargeState BuildUiState(
        PowerChargeComponent component,
        ApcPowerReceiverComponent powerReceiver,
        float chargeRate)
    {
        var chargeTarget = chargeRate < 0 ? 0 : component.MaxCharge;
        short chargeEta;
        var atTarget = false;
        if (MathHelper.CloseTo(component.Charge, chargeTarget))
        {
            chargeEta = short.MinValue; // N/A
            atTarget = true;
        }
        else
        {
            var diff = chargeTarget - component.Charge;
            chargeEta = (short) Math.Abs(diff / chargeRate);
        }

        var status = chargeRate switch
        {
            > 0 when atTarget => PowerChargePowerStatus.FullyCharged,
            < 0 when atTarget => PowerChargePowerStatus.Off,
            > 0 => PowerChargePowerStatus.Charging,
            < 0 => PowerChargePowerStatus.Discharging,
            _ => throw new ArgumentOutOfRangeException(nameof(chargeRate))
        };

        return new PowerChargeState(
            component.SwitchedOn,
            (byte) (component.Charge * 255),
            status,
            (short) Math.Round(powerReceiver.PowerReceived),
            (short) Math.Round(powerReceiver.Load),
            chargeEta
        );
    }

    private void UpdateUI(Entity<PowerChargeComponent, ApcPowerReceiverComponent> ent, float chargeRate)
    {
        var (_, component, powerReceiver) = ent;
        if (!_uiSystem.IsUiOpen(ent.Owner, component.UiKey))
            return;

        _uiSystem.SetUiState(ent.Owner, component.UiKey, BuildUiState(component, powerReceiver, chargeRate));
        component.NeedUIUpdate = false;
    }

    private void UpdateState(Entity<PowerChargeComponent, ApcPowerReceiverComponent> ent)
    {
        var (uid, machine, powerReceiver) = ent;
        var appearance = EntityManager.GetComponentOrNull<AppearanceComponent>(uid);
        _appearance.SetData(uid, PowerChargeVisuals.Charge, machine.Charge, appearance);
        _appearance.SetData(uid, PowerChargeVisuals.Active, machine.Active);


        if (!machine.Intact)
        {
            MakeBroken((uid, machine), appearance);
        }
        else if (powerReceiver.PowerReceived < machine.IdlePowerUse)
        {
            MakeUnpowered((uid, machine), appearance);
        }
        else if (!machine.SwitchedOn)
        {
            MakeOff((uid, machine), appearance);
        }
        else
        {
            MakeOn((uid, machine), appearance);
        }
    }

    private void MakeBroken(Entity<PowerChargeComponent> ent, AppearanceComponent? appearance)
    {
        _ambientSoundSystem.SetAmbience(ent, false);

        _appearance.SetData(ent, PowerChargeVisuals.State, PowerChargeStatus.Broken, appearance);
    }

    private void MakeUnpowered(Entity<PowerChargeComponent> ent, AppearanceComponent? appearance)
    {
        _ambientSoundSystem.SetAmbience(ent, false);

        _appearance.SetData(ent, PowerChargeVisuals.State, PowerChargeStatus.Unpowered, appearance);
    }

    private void MakeOff(Entity<PowerChargeComponent> ent, AppearanceComponent? appearance)
    {
        _ambientSoundSystem.SetAmbience(ent, false);

        _appearance.SetData(ent, PowerChargeVisuals.State, PowerChargeStatus.Off, appearance);
    }

    private void MakeOn(Entity<PowerChargeComponent> ent, AppearanceComponent? appearance)
    {
        _ambientSoundSystem.SetAmbience(ent, true);

        _appearance.SetData(ent, PowerChargeVisuals.State, PowerChargeStatus.On, appearance);
    }
}

[ByRefEvent] public record struct ChargedMachineActivatedEvent;
[ByRefEvent] public record struct ChargedMachineDeactivatedEvent;
