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
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
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
        if (comp.Passive == false)
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
        if (comp.Passive == false)
        {
            if (args.Handled) return;

            if (comp.IsPowered)
            {
                comp.IsPowered = false;
                _popup.PopupEntity(Loc.GetString("electrolyzer-turned-off"), uid, args.User);
                UpdateAppearance(uid);
            }
            else
            {
                TryTurnOn(uid, comp, args.User);
            }

            args.Handled = true;
        }
    }

    private void UpdateAppearance(EntityUid uid)
    {
        if (EntityManager.TryGetComponent<ElectrolyzerComponent>(uid, out var comp))
        {
            _appearance.SetData(uid, ElectrolyzerVisuals.State,
                comp.IsPowered ? ElectrolyzerState.On : ElectrolyzerState.Off);
        }
    }

    private void OnDeviceUpdated(EntityUid uid, ElectrolyzerComponent electrolyzer, ref AtmosDeviceUpdateEvent args, BatteryComponent battery)
    {
        if (electrolyzer.Passive == true)
        {
                comp.IsPowered = true;             
        }

        if (!Transform(uid).Anchored || !electrolyzer.IsPowered)
            return;

        if (electrolyzer.CurrentFuel <= 0f)
        {
            }
            // Get fuel value from sheet
            float fuelPerSheet = 0f;
            if (_tagSystem.HasTag(fuelEntity, PlasmaTag))
                fuelPerSheet = electrolyzer.PlasmaFuelConversion;
            else
                return;

            // Consume 1 sheet
            _stackSystem.SetCount((fuelEntity, stack), stack.Count - 1);
            electrolyzer.CurrentFuel = fuelPerSheet;

            // If stack now empty, delete it
            if (stack.Count <= 0)
                EntityManager.QueueDeleteEntity(fuelEntity);
        }

        var mixture = _atmosphereSystem.GetContainingMixture(uid, args.Grid, args.Map);
        if (mixture is null) return;

        var capicator = _battery.GetCharge(battery.Value.AsNullable());

        if (capicator <= 0f)
        return;

        var rate = Math.Min(1f, (capicator/200000f));    

        var initH2O = mixture.GetMoles(Gas.WaterVapor);
        var initHyperNob = mixture.GetMoles(Gas.HyperNoblium);
        var initBZ = mixture.GetMoles(Gas.BZ);
        var temperature = mixture.Temperature;
        var oldHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);

        var H2OLoad = 0;
        var HyperNobLoad = 0;
        var BZLoad = 0;

        if (initH2O > 0.05f)
        {
            var temperatureEfficiency = Math.Min(mixture.Temperature / 1123.15f, 1f); ///For some reason combustibles have variable oxy consumption? This keeps it balanced.

            var h2oRate = (float) Math.Min(2.5 * rate, initH2O / 2f);

            var h2oRemoved = h2oRate * 2f;
            var oxyProduced = h2oRate * temperatureEfficiency;
            var hydrogenProduced = h2oRate * 2f * temperatureEfficiency;

            mixture.AdjustMoles(Gas.WaterVapor, -h2oRemoved);
            mixture.AdjustMoles(Gas.Oxygen, oxyProduced);
            mixture.AdjustMoles(Gas.Hydrogen, hydrogenProduced);

            H2OLoad = (int) (Atmospherics.FireHydrogenEnergyReleased * hydrogenProduced); ///Load is determined by the energy made by re-igniting the hydrogen. Efficiency of device prevents free power.
        }

        if (initHyperNob > 0.01f && temperature < 150f)
        {
            var HNobRate = (float) Math.Min((1.5 * rate), initHyperNob);

            mixture.AdjustMoles(Gas.HyperNoblium, -HNobRate);
            mixture.AdjustMoles(Gas.AntiNoblium, HNobRate);

            HyperNobLoad = (int) (5000f * HNobRate); ///High energy consumption.
        }

        if (initBZ > 0.01f)
        {
            var BZRate = (float) Math.Min(2.5 * rate, initBZ);

            mixture.AdjustMoles(Gas.BZ, -BZRate);
            mixture.AdjustMoles(Gas.Oxygen, BZRate * 0.2f);
            mixture.AdjustMoles(Gas.Halon, BZRate * 2f);
            var energyReleased = BZRate * Atmospherics.HalonProductionEnergy;

            var newHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

            BZLoad = (int) (BZRate); ///Low energy consumption since overall its actually making more energy in thermal power.
        }

        var finalHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);
        if (finalHeatCapacity > Atmospherics.MinimumHeatCapacity && finalHeatCapacity != oldHeatCapacity)
            mixture.Temperature = Math.Max(mixture.Temperature * oldHeatCapacity / finalHeatCapacity, Atmospherics.TCMB);

        var powerUsed = (500f + H2OLoad + HyperNobLoad + BZLoad);

        var fuelMultiplier = 1f;

        if (electrolyzer.CurrentFuel >= 0f)
        {
            fuelMultiplier = 0.1f;
        }

        _battery.ChangeCharge(battery.Value.AsNullable(), (-powerUsed * fuelMultiplier) / electrolyzer.Efficiency); ///NOT WORKING!!! HLEP!!!

        electrolyzer.CurrentFuel = Math.Max(0f, electrolyzer.CurrentFuel - (powerUsed - 500f));

        if (electrolyzer.Passive == true)
        {
                comp.IsPowered = false;             
        }

        _gasOverlaySystem.UpdateSessions();
    }

    private void OnInteractUsingFuel(EntityUid uid, ElectrolyzerComponent comp, InteractUsingEvent args)
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
        if ((heldIsPlasma && existingIsPlasma))
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

    private void TryTurnOn(EntityUid uid, ElectrolyzerComponent comp, EntityUid? user = null)
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

        bool hasFuel = comp.CurrentFuel > 0f ||
                       (_itemSlots.TryGetSlot(uid, "fuel", out var slot) &&
                       slot.ContainerSlot?.ContainedEntity != null);

        if (!hasFuel)
        {
            _popup.PopupEntity(Loc.GetString("electrolyzer-no-fuel"), uid);
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
