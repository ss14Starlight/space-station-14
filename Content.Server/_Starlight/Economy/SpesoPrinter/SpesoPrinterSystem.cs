using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Shared._Starlight.Economy.SpesoPrinter;
using Content.Shared.Atmos;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Economy.SpesoPrinter;

public sealed class SpesoPrinterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpesoPrinterComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, SpesoPrinterComponent component, MapInitEvent args)
    {
        component.NextPrintTime = _timing.CurTime + TimeSpan.FromSeconds(component.BasePrintInterval);
        UpdatePowerDraw(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SpesoPrinterComponent, PowerConsumerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var printer, out var consumer, out var xform))
        {
            if (!printer.Enabled)
                continue;

            if (!xform.Anchored)
                continue;

            // Check if receiving power from HV network
            var receivingPower = consumer.NetworkLoad.ReceivingPower;
            var requiredPower = GetRequiredPower(printer);
            var hasPower = receivingPower >= requiredPower * 0.9f; // 90% tolerance

            if (!hasPower)
            {
                if (printer.Printing || printer.WasPowered)
                {
                    printer.Printing = false;
                    printer.WasPowered = false;
                    UpdateVisuals(uid, printer, false);
                    Dirty(uid, printer);
                }
                continue;
            }

            // Check atmospheric pressure
            var environment = _atmosphere.GetContainingMixture(uid, true, true);
            var pressure = environment?.Pressure ?? 0f;
            var hasAtmosphere = pressure >= printer.MinPressure;

            if (!hasAtmosphere)
            {
                if (printer.Printing || printer.WasPowered)
                {
                    printer.Printing = false;
                    printer.WasPowered = false;
                    UpdateVisuals(uid, printer, false);
                    Dirty(uid, printer);
                }

                // Play warning beep periodically
                if (_timing.CurTime >= printer.NextWarningBeep)
                {
                    _audio.PlayPvs(printer.WarningBeep, uid);
                    printer.NextWarningBeep = _timing.CurTime + TimeSpan.FromSeconds(printer.WarningBeepInterval);
                }
                continue;
            }

            if (!printer.WasPowered)
            {
                printer.WasPowered = true;
                printer.NextHeatTime = _timing.CurTime + TimeSpan.FromSeconds(printer.HeatInterval);
                UpdateVisuals(uid, printer, true);
                Dirty(uid, printer);
            }

            // Generate heat continuously while powered
            if (_timing.CurTime >= printer.NextHeatTime)
            {
                GenerateHeat(uid, printer);
                printer.NextHeatTime = _timing.CurTime + TimeSpan.FromSeconds(printer.HeatInterval);
            }

            // If currently printing, check if animation is done
            if (printer.Printing)
            {
                if (_timing.CurTime >= printer.PrintingEndTime)
                {
                    printer.Printing = false;
                    UpdateVisuals(uid, printer, true);

                    var spawnCoords = xform.Coordinates.Offset(xform.LocalRotation.RotateVec(printer.SpawnOffset));
                    var cashAmount = printer.BaseCreditsPerPrint + (printer.CreditsIncreasePerLevel * printer.PrintLevel);;
                    _stack.Spawn(cashAmount, printer.CashStackType, spawnCoords);

                    if (printer.PrintLevel < printer.MaxPrintLevel)
                    {
                        printer.PrintLevel++;
                        UpdatePowerDraw(uid, printer);
                    }

                    // Calculate next print time based on current level
                    var interval = CalculatePrintInterval(printer);
                    printer.NextPrintTime = _timing.CurTime + TimeSpan.FromSeconds(interval);

                    Dirty(uid, printer);
                }
                continue;
            }

            if (_timing.CurTime >= printer.NextPrintTime)
            {
                printer.Printing = true;
                printer.PrintingEndTime = _timing.CurTime + TimeSpan.FromSeconds(printer.PrintAnimationDuration);
                UpdateVisuals(uid, printer, true);
                _audio.PlayPvs(printer.PrintSound, uid);
                Dirty(uid, printer);
            }
        }
    }

    private float CalculatePrintInterval(SpesoPrinterComponent printer)
    {
        var interval = printer.BasePrintInterval * MathF.Pow(printer.IntervalDecreasePerLevel, printer.PrintLevel);
        return MathF.Max(interval, printer.MinPrintInterval);
    }

    private float GetRequiredPower(SpesoPrinterComponent printer) => printer.BasePowerDraw * MathF.Pow(printer.PowerIncreasePerLevel, printer.PrintLevel);

    private void UpdatePowerDraw(EntityUid uid, SpesoPrinterComponent printer)
    {
        if (!TryComp<PowerConsumerComponent>(uid, out var consumer))
            return;

        consumer.DrawRate = GetRequiredPower(printer);
    }

    private void GenerateHeat(EntityUid uid, SpesoPrinterComponent printer)
    {
        var environment = _atmosphere.GetContainingMixture(uid, true, true);
        if (environment == null)
            return;

        var heatAmount = printer.BaseHeatPerTick * MathF.Pow(printer.HeatIncreasePerLevel, printer.PrintLevel);

        // Add heat energy to the surrounding atmosphere
        if (environment.TotalMoles > 0)
        {
            var heatCapacity = _atmosphere.GetHeatCapacity(environment, true);
            if (heatCapacity > Atmospherics.MinimumHeatCapacity)
            {
                environment.Temperature += heatAmount / heatCapacity;
            }
        }
    }

    private void UpdateVisuals(EntityUid uid, SpesoPrinterComponent printer, bool powered)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;
        _appearance.SetData(uid, SpesoPrinterComponent.SpesoPrinterVisuals.Powered, powered, appearance);

        _appearance.SetData(uid, SpesoPrinterComponent.SpesoPrinterVisuals.Printing, printer.Printing, appearance);
    }
}
