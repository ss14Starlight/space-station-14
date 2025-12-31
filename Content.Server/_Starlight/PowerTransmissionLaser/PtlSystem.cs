using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Popups;
using Content.Shared._Starlight.PowerTransmissionLaser;
using Content.Shared.Power.Components;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.PowerTransmissionLaser;

public sealed class PtlSystem : EntitySystem
{
    [Dependency] private readonly PowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private const float WattsPerMegawatt = 1_000_000f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PtlComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<PtlComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<PtlComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpened);

        SubscribeLocalEvent<PtlComponent, PtlSetEnabledMessage>(OnSetEnabled);
        SubscribeLocalEvent<PtlComponent, PtlSetPowerMessage>(OnSetPower);
    }

    private void OnInit(EntityUid uid, PtlComponent comp, MapInitEvent args)
    {
        UpdatePowerLoad(uid, comp);
        UpdateAppearance(uid, comp);
    }

    private void OnPowerChanged(EntityUid uid, PtlComponent comp, ref PowerChangedEvent args)
    {
        UpdateAppearance(uid, comp);
        DirtyUi(uid, comp);
    }

    private void OnBeforeUiOpened(EntityUid uid, PtlComponent comp, BeforeActivatableUIOpenEvent args) => DirtyUi(uid, comp);

    private void OnSetEnabled(EntityUid uid, PtlComponent comp, PtlSetEnabledMessage args)
    {
        if (args.Enabled && comp.TargetPowerMw <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("ptl-ui-error-power-zero"), uid, args.Actor);
            comp.Enabled = false;
        }
        else
        {
            comp.Enabled = args.Enabled;
        }

        UpdatePowerLoad(uid, comp);
        UpdateAppearance(uid, comp);
        DirtyUi(uid, comp);
    }

    private void OnSetPower(EntityUid uid, PtlComponent comp, PtlSetPowerMessage args)
    {
        var clamped = Math.Clamp(args.TargetPowerMw, comp.MinPowerMw, comp.MaxPowerMw);
        comp.TargetPowerMw = clamped;

        if (comp.Enabled && comp.TargetPowerMw <= 0f)
        {
            comp.Enabled = false;
            _popup.PopupEntity(Loc.GetString("ptl-ui-error-power-zero"), uid, args.Actor);
        }

        UpdatePowerLoad(uid, comp);
        DirtyUi(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PtlComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled)
                continue;

            if (!TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
                continue;

            comp.Accumulator += frameTime;
            if (comp.Accumulator < comp.CycleTimeSeconds)
                continue;

            while (comp.Accumulator >= comp.CycleTimeSeconds)
            {
                comp.Accumulator -= comp.CycleTimeSeconds;
                RunCycle(uid, comp, receiver);

                if (!comp.Enabled)
                    break;
            }
        }
    }

    private void RunCycle(EntityUid uid, PtlComponent comp, ApcPowerReceiverComponent receiver)
    {
        if (comp.TargetPowerMw <= 0f)
            return;

        var targetWatts = comp.TargetPowerMw * WattsPerMegawatt;
        var targetEnergyJ = targetWatts * comp.CycleTimeSeconds;

        var powered = _powerReceiver.IsPowered(uid, receiver);
        float actualEnergyUsedJ;

        if (powered)
        {
            actualEnergyUsedJ = targetEnergyJ;
        }
        else
        {
            // Fallback to internal battery.
            if (!TryComp<BatteryComponent>(uid, out var battery))
            {
                comp.Enabled = false;
                UpdatePowerLoad(uid, comp);
                UpdateAppearance(uid, comp);
                DirtyUi(uid, comp);
                return;
            }

            if (_battery.GetCharge((uid, battery)) <= 0f)
            {
                comp.Enabled = false;
                UpdatePowerLoad(uid, comp);
                UpdateAppearance(uid, comp);
                DirtyUi(uid, comp);
                return;
            }

            var delta = _battery.UseCharge((uid, battery), targetEnergyJ);
            actualEnergyUsedJ = -delta;

            if (_battery.GetCharge((uid, battery)) <= 0.001f)
            {
                _battery.SetCharge((uid, battery), 0f);
                comp.Enabled = false;
                UpdatePowerLoad(uid, comp);
                UpdateAppearance(uid, comp);
            }
        }

        var spesosPerJoule = comp.SpesosPerMwPerCycle / (WattsPerMegawatt * comp.CycleTimeSeconds);
        var earned = (double) actualEnergyUsedJ * spesosPerJoule;

        comp.SpesoCarry += earned;
        var whole = (int) Math.Floor(comp.SpesoCarry);
        if (whole > 0)
        {
            comp.TotalSpesosEarned += whole;
            comp.SpesoCarry -= whole;
        }

        DirtyUi(uid, comp);
    }

    private void UpdatePowerLoad(EntityUid uid, PtlComponent comp)
    {
        if (!TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            return;

        var loadWatts = comp.Enabled ? comp.TargetPowerMw * WattsPerMegawatt : 0f;
        _powerReceiver.SetLoad((uid, receiver), loadWatts);
    }

    private void UpdateAppearance(EntityUid uid, PtlComponent comp) => _appearance.SetData(uid, PtlVisuals.Active, comp.Enabled);

    private void DirtyUi(EntityUid uid, PtlComponent comp)
    {
        if (!_ui.HasUi(uid, PtlUiKey.Key))
            return;

        var batteryCurrent = 0f;
        var batteryMax = 0f;
        if (TryComp<BatteryComponent>(uid, out var battery))
        {
            batteryCurrent = battery.CurrentCharge;
            batteryMax = battery.MaxCharge;
        }

        _ui.SetUiState(uid, PtlUiKey.Key, new PtlBoundUserInterfaceState(
            comp.Enabled,
            batteryCurrent,
            batteryMax,
            comp.TargetPowerMw,
            comp.MinPowerMw,
            comp.MaxPowerMw,
            comp.TotalSpesosEarned));
    }
}
