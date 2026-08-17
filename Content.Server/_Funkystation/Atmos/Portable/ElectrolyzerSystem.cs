using Content.Server.Power.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Popups;
using Content.Shared.Atmos;
using Content.Shared._Funkystation.Atmos.Visuals;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Content.Server.Hands.Systems;
using Content.Shared.Tag;
using Content.Shared.Hands.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Robust.Server.Audio;
using Robust.Shared.Audio;

namespace Content.Server._Funkystation.Atmos.Portable;

public sealed partial class ElectrolyzerSystem : EntitySystem
{

    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private GasTileOverlaySystem _gasOverlaySystem = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private StackSystem _stackSystem = default!;
    [Dependency] private HandsSystem _handsSystem = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!; /// Starlight: Needed for electric charging
    [Dependency] private PowerCellSystem _powerCell = default!; /// Starlight: ''
    private const string PlasmaTag = "SheetPlasma"; // Starlight Edit: PlasmaSheet -> SheetPlasma

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ElectrolyzerComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<ElectrolyzerComponent, AtmosDeviceUpdateEvent>(OnDeviceUpdated);
        SubscribeLocalEvent<ElectrolyzerComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ElectrolyzerComponent, InteractUsingEvent>(OnInteractUsingFuel);
        SubscribeLocalEvent<ElectrolyzerComponent, AnchorStateChangedEvent>(OnAnchorChanged);

    }

    private void OnSignalReceived(EntityUid uid, ElectrolyzerComponent comp, SignalReceivedEvent args)
    {
        if (comp.Passive == false) /// Starlight: Only check active electrolyzers for signals.
        {
                if (!TryComp<DeviceLinkSinkComponent>(uid, out _))
                    return;

                bool? newState;

                switch (args.Port)
                {
                    case "On":
                        newState = true;
                        break;
                    case "Off":
                        newState = false;
                        break;
                    case "Toggle":
                        newState = !comp.IsPowered;
                        break;
                    default:
                        return;
                }

                if (newState == comp.IsPowered)
                    return;

                if (newState == true)
                {
                    TryTurnOn(uid, comp);
                }
                else
                {
                    comp.IsPowered = false;
                    UpdateAppearance(uid);
                }
        }
    }

    private void OnActivate(EntityUid uid, ElectrolyzerComponent comp, ActivateInWorldEvent args)
    {
        if (comp.Passive == true) /// Starlight: Don't try to activate passive electrolyzers
                    return;

        if (args.Handled) return;

        if (!TryComp<BatteryComponent>(uid, out var battery)) ///Starlight: Electricity required
            return;

        var charge = _battery.GetCharge((uid, battery));

        if (comp.IsPowered)
        {
            comp.IsPowered = false;
            _popup.PopupEntity(Loc.GetString("electrolyzer-turned-off"), uid, args.User);
            UpdateAppearance(uid);
        }
        else
        {
            if (charge <= 0f) ///Starlight: If battery is zero, can't turn on. Doesn't force already turned on electolyzers off in case of power shortfall because that would annoying.
                return;
            TryTurnOn(uid, comp, args.User);
        }

        args.Handled = true;
    }

    private void UpdateAppearance(EntityUid uid)
    {
        if (EntityManager.TryGetComponent<ElectrolyzerComponent>(uid, out var comp))
        {
            _appearance.SetData(uid, ElectrolyzerVisuals.State,
                comp.IsPowered ? ElectrolyzerState.On : ElectrolyzerState.Off);
        }
    }

    private void OnDeviceUpdated(EntityUid uid, ElectrolyzerComponent electrolyzer, ref AtmosDeviceUpdateEvent args)
    {
        if (!TryComp<BatteryComponent>(uid, out var battery)) ///Starlight: Electricity required
            return;

        var charge = _battery.GetCharge((uid, battery));

        if (!electrolyzer.Passive) /// Starlight: Only draw grid energy for active electrolyzers.
        {
            if (!TryComp<PowerConsumerComponent>(uid, out var powerConsumer))
                    return;

                if (!Transform(uid).Anchored)
                {
                    powerConsumer.DrawRate = 0f;
                    return;
                }

                var missingCharge = battery.MaxCharge - charge;
                powerConsumer.DrawRate = Math.Min(50_000f, Math.Max(0f, missingCharge));
                _battery.ChangeCharge((uid, battery), powerConsumer.ReceivedPower * args.dt);
                charge = _battery.GetCharge((uid, battery));
        }

        if (electrolyzer.Passive == true)
        {
            electrolyzer.IsPowered = true;
        }

        if (charge <= 0f)
        return;

        if (!Transform(uid).Anchored || !electrolyzer.IsPowered)
            return;

        var mixture = _atmosphereSystem.GetContainingMixture(uid, args.Grid, args.Map);
        if (mixture is null) return;

        if (electrolyzer.Passive == false) /// Starlight: Fuel handling now optional, and doesnt check for passive electrolyzers.
        {
            if (electrolyzer.CurrentFuel <= 0f && _itemSlots.TryGetSlot(uid, "fuel", out var slot) && slot.ContainerSlot?.ContainedEntity is { } fuelEntity && TryComp<StackComponent>(fuelEntity, out var stack) && stack.Count > 0 && _tagSystem.HasTag(fuelEntity, PlasmaTag))
            {
                var remaining = stack.Count - 1;
                _stackSystem.SetCount((fuelEntity, stack), remaining);
                electrolyzer.CurrentFuel = electrolyzer.PlasmaFuelConversion;

                if (remaining <= 0)
                EntityManager.QueueDeleteEntity(fuelEntity);
            }
        }

        var rate = (charge/battery.MaxCharge) * args.dt;
        var initH2O = mixture.GetMoles(Gas.WaterVapor);
        var initHyperNob = mixture.GetMoles(Gas.HyperNoblium);
        var initBZ = mixture.GetMoles(Gas.BZ);
        var temperature = mixture.Temperature;
        var oldHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);

        var H2OLoad = 0; ///Starlight: Dummy values, load is now combined rather than highest wins.
        var HyperNobLoad = 0;
        var BZLoad = 0;
        var heatScale = _atmosphereSystem.HeatScale;

        if (initH2O > 0.05f)
        {
            var temperatureEfficiency = Math.Min(mixture.Temperature / 1123.15f, 1f); ///Starlight: For some reason combustibles have variable oxy consumption? This keeps it balanced.
            var h2oRate = Math.Min(Math.Min(2.5f * rate, initH2O / 2f), (2f * charge / electrolyzer.Efficiency)/(Atmospherics.FireHydrogenEnergyReleased / heatScale));

            var h2oRemoved = h2oRate * 2f;
            var oxyProduced = h2oRate * temperatureEfficiency;
            var hydrogenProduced = h2oRate * 2f * temperatureEfficiency;

            mixture.AdjustMoles(Gas.WaterVapor, -h2oRemoved);
            mixture.AdjustMoles(Gas.Oxygen, oxyProduced);
            mixture.AdjustMoles(Gas.Hydrogen, hydrogenProduced);

            H2OLoad = (int) ((Atmospherics.FireHydrogenEnergyReleased / heatScale) * hydrogenProduced); ///Starlight: Load is determined by the energy made by re-igniting the hydrogen. Efficiency of device prevents free power.
        }

        if (initHyperNob > 0.01f && temperature < 150f)
        {
            var HNobRate = (float) Math.Min((1.5 * rate), initHyperNob);

            mixture.AdjustMoles(Gas.HyperNoblium, -HNobRate);
            mixture.AdjustMoles(Gas.AntiNoblium, HNobRate);

            HyperNobLoad = (int) (5000f * HNobRate); ///Starlight: High energy consumption.
        }

        if (initBZ > 0.01f)
        {
            var BZRate = (float) Math.Min(2.5 * rate, initBZ);

            mixture.AdjustMoles(Gas.BZ, -BZRate);
            mixture.AdjustMoles(Gas.Oxygen, BZRate * 0.2f);
            mixture.AdjustMoles(Gas.Halon, BZRate * 2f);
            var energyReleased = BZRate * (Atmospherics.HalonProductionEnergy / heatScale);

            var newHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

            BZLoad = (int) (BZRate); ///Starlight: Low energy consumption since overall its actually making more energy in thermal power.
        }

        var finalHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);
        if (finalHeatCapacity > Atmospherics.MinimumHeatCapacity && finalHeatCapacity != oldHeatCapacity)
            mixture.Temperature = Math.Max(mixture.Temperature * oldHeatCapacity / finalHeatCapacity, Atmospherics.TCMB);

        var powerUsed = (50f + H2OLoad + HyperNobLoad + BZLoad); ///Starlight: Gotta consume power

        var fuelMultiplier = 1f;

        var PlasmaFuel = electrolyzer.CurrentFuel;

        if (PlasmaFuel > 0f)
        {
            fuelMultiplier = 0.01f;
        }

        _battery.ChangeCharge((uid, battery),-powerUsed * fuelMultiplier / electrolyzer.Efficiency);

        electrolyzer.CurrentFuel = Math.Max(0f, electrolyzer.CurrentFuel - (powerUsed * 0.01f));

        if (electrolyzer.Passive == true)
        {
                electrolyzer.IsPowered = false;
        }

        _gasOverlaySystem.UpdateSessions();
    }

    private void OnInteractUsingFuel(EntityUid uid, ElectrolyzerComponent comp, InteractUsingEvent args)
    {
        if (comp.Passive == false) ///Starlight: Don't put fuel inside passive electrolyzers
        {
            if (args.Handled || args.Target != uid)
                return;

            if (!_itemSlots.TryGetSlot(uid, "fuel", out var slot) || slot.ContainerSlot == null)
                return;

            var heldItem = args.Used;
            var existingItem = slot.ContainerSlot.ContainedEntity;

            // Tag checks
            bool heldIsPlasma = _tagSystem.HasTag(heldItem, PlasmaTag);

            if (!heldIsPlasma)
                return;

            args.Handled = true;

            if (existingItem == null)
            {
                // Empty: insert normally
                if (_itemSlots.TryInsert(uid, "fuel", heldItem, args.User))
                {
                    _popup.PopupEntity(Loc.GetString("electrolyzer-fuel-inserted"), uid, args.User);
                }
                return;
            }

                bool existingIsPlasma = _tagSystem.HasTag(existingItem.Value, PlasmaTag);

                // Same type: merge
                if ((heldIsPlasma && existingIsPlasma)) ///Starlight: Uranium no longer a valid solid fuel.
                {
                    if (!TryComp<StackComponent>(heldItem, out var heldStack) ||
                        !TryComp<StackComponent>(existingItem.Value, out var existingStack))
                    {
                        _popup.PopupEntity(Loc.GetString("electrolyzer-cannot-merge-invalid-stack"), uid, args.User); // Should never happen
                        return;
                    }

                    int maxStack = _stackSystem.GetMaxCount(existingStack);
                    int total = existingStack.Count + heldStack.Count;

                    if (total > maxStack)
                    {
                        int toAdd = maxStack - existingStack.Count;
                        _stackSystem.SetCount((existingItem.Value, existingStack), maxStack);
                        _stackSystem.SetCount((heldItem, heldStack), heldStack.Count - toAdd);
                    }
                    else
                    {
                        _stackSystem.SetCount((existingItem.Value, existingStack), total);
                        EntityManager.QueueDeleteEntity(heldItem);
                    }

                    return;
                }
        }
    }

    private void TryTurnOn(EntityUid uid, ElectrolyzerComponent comp, EntityUid? user = null)
    {
        if (comp.Passive == false) ///Starlight: Can't toggle passive electrolyzers.
        {
            if (comp.IsPowered)
                return;

            if (!Transform(uid).Anchored)
            {
                if (user != null)
                {
                    _popup.PopupEntity(Loc.GetString("electrolyzer-must-be-anchored"), uid, user.Value);
                }
                return;
            }

            comp.IsPowered = true;

            _popup.PopupEntity(Loc.GetString("electrolyzer-turned-on"), uid);

            if (comp.OnSound != null)
            {
                _audio.PlayPvs(comp.OnSound, uid, AudioParams.Default.WithVolume(-4f));
            }

            UpdateAppearance(uid);

        }
    }

    private void OnAnchorChanged(EntityUid uid, ElectrolyzerComponent comp, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored && comp.IsPowered)
        {
            comp.IsPowered = false;
            UpdateAppearance(uid);
            _popup.PopupEntity(Loc.GetString("electrolyzer-turned-off"), uid);
        }
    }
}
