using Content.Server.Cargo.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Power.PowerTransmissionLaser;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Power.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Content.Shared.Power;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Power.PowerTransmissionLaser;

public sealed class PowerTransmissionLaserSystem : EntitySystem
{
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private const float WattsPerMegawatt = 1_000_000f;
    private static readonly ProtoId<CargoAccountPrototype> _engineeringAccount = "Engineering";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerTransmissionLaserComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<PowerTransmissionLaserComponent, PowerConsumerReceivedChanged>(OnPowerConsumerReceivedChanged);
        SubscribeLocalEvent<BatteryComponent, ChargeChangedEvent>(OnBatteryChargeChanged);

        SubscribeLocalEvent<PowerTransmissionLaserComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpened);

        SubscribeLocalEvent<PowerTransmissionLaserComponent, PowerTransmissionLaserSetEnabledMessage>(OnSetEnabled);
        SubscribeLocalEvent<PowerTransmissionLaserComponent, PowerTransmissionLaserSetPowerMessage>(OnSetPower);
    }

    private void OnBatteryChargeChanged(EntityUid uid, BatteryComponent comp, ref ChargeChangedEvent args)
    {
        if (!TryComp<PowerTransmissionLaserComponent>(uid, out var powerTransmissionLaser))
            return;

        DirtyUi(uid, powerTransmissionLaser);
    }

    private void OnInit(EntityUid uid, PowerTransmissionLaserComponent comp, MapInitEvent args)
    {
        UpdatePowerLoad(uid, comp);
        UpdateAppearance(uid, comp);
    }

    private void OnPowerConsumerReceivedChanged(EntityUid uid, PowerTransmissionLaserComponent comp, ref PowerConsumerReceivedChanged args)
    {
        UpdateAppearance(uid, comp);
        DirtyUi(uid, comp);
    }

    private void OnBeforeUiOpened(EntityUid uid, PowerTransmissionLaserComponent comp, BeforeActivatableUIOpenEvent args) => DirtyUi(uid, comp);

    private void OnSetEnabled(EntityUid uid, PowerTransmissionLaserComponent comp, PowerTransmissionLaserSetEnabledMessage args)
    {
        var oldEnabled = comp.Enabled;

        if (args.Enabled && comp.TargetPowerMw <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("ptl-ui-error-power-zero"), uid, args.Actor);
            SetEnabled(uid, comp, false);
        }else{
            SetEnabled(uid, comp, args.Enabled);
        }

        if (oldEnabled && comp.Enabled)
            return;

        UpdatePowerLoad(uid, comp);
        UpdateAppearance(uid, comp);
        DirtyUi(uid, comp);
    }

    private void OnSetPower(EntityUid uid, PowerTransmissionLaserComponent comp, PowerTransmissionLaserSetPowerMessage args)
    {
        var clamped = Math.Clamp(args.TargetPowerMw, comp.MinPowerMw, comp.MaxPowerMw);
        comp.TargetPowerMw = clamped;

        if (comp.Enabled && comp.TargetPowerMw <= 0f)
        {
            SetEnabled(uid, comp, false);
            _popup.PopupEntity(Loc.GetString("ptl-ui-error-power-zero"), uid, args.Actor);
        }

        UpdatePowerLoad(uid, comp);
        DirtyUi(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PowerTransmissionLaserComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled || !TryComp<PowerConsumerComponent>(uid, out var consumer))
                continue;

            if (comp.CycleTimeSeconds <= 0f)
            {
                SetEnabled(uid, comp, false);
                UpdatePowerLoad(uid, comp);
                UpdateAppearance(uid, comp);
                DirtyUi(uid, comp);
                continue;
            }

            comp.Accumulator = MathF.Min(comp.Accumulator + frameTime, comp.CycleTimeSeconds);
            if (comp.Accumulator < comp.CycleTimeSeconds)
                continue;
            comp.Accumulator -= comp.CycleTimeSeconds;
            RunCycle(uid, comp, consumer);
        }
    }

    private void RunCycle(EntityUid uid, PowerTransmissionLaserComponent comp, PowerConsumerComponent consumer)
    {
        if (comp.TargetPowerMw <= 0f)
            return;

        var targetWatts = (double) comp.TargetPowerMw * WattsPerMegawatt;
        var targetEnergyJ = targetWatts * comp.CycleTimeSeconds;

        var powered = consumer.ReceivedPower > 0.001f;
        double actualEnergyUsedJ;

        if (powered)
        {
            actualEnergyUsedJ = targetEnergyJ;
        }
        else
        {
            if (!TryComp<BatteryComponent>(uid, out var battery))
            {
                SetEnabled(uid, comp, false);
                UpdatePowerLoad(uid, comp);
                UpdateAppearance(uid, comp);
                DirtyUi(uid, comp);
                return;
            }

            if (_battery.GetCharge((uid, battery)) <= 0f)
            {
                SetEnabled(uid, comp, false);
                UpdatePowerLoad(uid, comp);
                UpdateAppearance(uid, comp);
                DirtyUi(uid, comp);
                return;
            }

            var delta = _battery.UseCharge((uid, battery), (float) targetEnergyJ);
            actualEnergyUsedJ = -(double) delta;

            if (_battery.GetCharge((uid, battery)) <= 0.001f)
            {
                _battery.SetCharge((uid, battery), 0f);
                SetEnabled(uid, comp, false);
                UpdatePowerLoad(uid, comp);
                UpdateAppearance(uid, comp);
            }
        }

        var fraction = targetEnergyJ <= 0.0 ? 0.0 : actualEnergyUsedJ / targetEnergyJ;
        var earned = (double) comp.TargetPowerMw * comp.SpesosPerMwPerCycle * fraction;

        comp.SpesoCarry += earned;
        var whole = (int) Math.Floor(comp.SpesoCarry + 1e-9);
        if (whole > 0)
        {
            comp.TotalSpesosEarned += whole;

            var stationUid = _station.GetOwningStation(uid);
            if (stationUid != null && TryComp<StationBankAccountComponent>(stationUid, out var bank))
            {
                if (TryGetEngineeringOrderConsoleAccount(stationUid.Value, out var engineeringAccount))
                    _cargo.UpdateBankAccount((stationUid.Value, bank), whole, engineeringAccount);
                else
                    _cargo.UpdateBankAccount((stationUid.Value, bank), whole, bank.PrimaryAccount);
            }

            comp.SpesoCarry -= whole;
        }

        DirtyUi(uid, comp);
    }

    private void UpdatePowerLoad(EntityUid uid, PowerTransmissionLaserComponent comp)
    {
        if (!TryComp<PowerConsumerComponent>(uid, out var consumer))
            return;

        var loadWatts = comp.Enabled ? comp.TargetPowerMw * WattsPerMegawatt : 0f;
        consumer.DrawRate = loadWatts;
    }

    private void UpdateAppearance(EntityUid uid, PowerTransmissionLaserComponent comp) => _appearance.SetData(uid, PowerTransmissionLaserVisuals.Active, comp.Enabled);

    private void SetEnabled(EntityUid uid, PowerTransmissionLaserComponent comp, bool enabled)
    {
        if (comp.Enabled == enabled)
            return;

        comp.Enabled = enabled;

        if (enabled)
        {
            _audio.PlayPvs(comp.StartSound, uid, AudioParams.Default.WithVolume(-3f).WithMaxDistance(7f));
            comp.PlayingStream = _audio.Stop(comp.PlayingStream);
            comp.PlayingStream = _audio.PlayPvs(comp.LoopingSound, uid,
                AudioParams.Default.WithLoop(true).WithVolume(2f).WithMaxDistance(10f))?.Entity;
        }
        else
        {
            _audio.PlayPvs(comp.StartSound, uid, AudioParams.Default.WithVolume(-3f).WithMaxDistance(7f));
            comp.PlayingStream = _audio.Stop(comp.PlayingStream);
        }
    }

    private void DirtyUi(EntityUid uid, PowerTransmissionLaserComponent comp)
    {
        if (!_ui.HasUi(uid, PowerTransmissionLaserUiKey.Key))
            return;

        var batteryCurrent = 0f;
        var batteryMax = 0f;
        if (TryComp<BatteryComponent>(uid, out var battery))
        {
            batteryCurrent = battery.CurrentCharge;
            batteryMax = battery.MaxCharge;
        }

        var gridSaturation = 0f;

        if (TryComp<PowerConsumerComponent>(uid, out var consumer))
        {
            var load = consumer.DrawRate;
            if (load > 0f)
                gridSaturation = Math.Clamp(consumer.ReceivedPower / load, 0f, 1f);
        }

        _ui.SetUiState(uid, PowerTransmissionLaserUiKey.Key, new PowerTransmissionLaserBoundUserInterfaceState(
            comp.Enabled,
            batteryCurrent,
            batteryMax,
            0f,
            0f,
            gridSaturation,
            comp.TargetPowerMw,
            comp.MinPowerMw,
            comp.MaxPowerMw,
            comp.TotalSpesosEarned));
    }

    private bool TryGetEngineeringOrderConsoleAccount(EntityUid stationUid, out string account)
    {
        var query = EntityQueryEnumerator<CargoOrderConsoleComponent>();
        while (query.MoveNext(out var consoleUid, out var console))
        {
            if (console.Account != _engineeringAccount)
                continue;

            var consoleStation = _station.GetOwningStation(consoleUid);
            if (consoleStation != stationUid)
                continue;

            account = console.Account.Id;
            return true;
        }

        account = string.Empty;
        return false;
    }
}
