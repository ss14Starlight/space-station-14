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

public sealed class ElectrolyzerSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly GasTileOverlaySystem _gasOverlaySystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    private const float WorkingPower = 2f;
    private const float PowerEfficiency = 1f;
    private const string PlasmaTag = "PlasmaSheet";
    private const string UraniumTag = "UraniumSheet";

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

    private void OnActivate(EntityUid uid, ElectrolyzerComponent comp, ActivateInWorldEvent args)
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
        if (!Transform(uid).Anchored || !electrolyzer.IsPowered)
            return;

        if (electrolyzer.CurrentFuel <= 0f)
        {
            if (!_itemSlots.TryGetSlot(uid, "fuel", out var slot) || slot.ContainerSlot?.ContainedEntity is not { } fuelEntity)
            {
                electrolyzer.IsPowered = false; // auto-shutdown if no more fuel possible
                UpdateAppearance(uid);
                _popup.PopupEntity(Loc.GetString("electrolyzer-no-fuel"), uid);
                return;
            }

            if (!TryComp<StackComponent>(fuelEntity, out var stack) || stack.Count <= 0)
            {
                electrolyzer.IsPowered = false;
                UpdateAppearance(uid);
                _popup.PopupEntity(Loc.GetString("electrolyzer-no-fuel"), uid);
                return;
            }

            // Determine fuel value per sheet
            float fuelPerSheet = 0f;
            if (_tagSystem.HasTag(fuelEntity, PlasmaTag))
                fuelPerSheet = electrolyzer.PlasmaFuelConversion;
            else if (_tagSystem.HasTag(fuelEntity, UraniumTag))
                fuelPerSheet = electrolyzer.UraniumFuelConversion;
            else
                return;

            // Consume 1 sheet
            _stackSystem.SetCount((fuelEntity, stack), stack.Count - 1);
            electrolyzer.CurrentFuel = fuelPerSheet;

            // If stack now empty, delete it
            if (stack.Count <= 0)
                EntityManager.QueueDeleteEntity(fuelEntity);
        }

        UpdateAppearance(uid);

        var mixture = _atmosphereSystem.GetContainingMixture(uid, args.Grid, args.Map);
        if (mixture is null) return;

        var initH2O = mixture.GetMoles(Gas.WaterVapor);
        var initHyperNob = mixture.GetMoles(Gas.HyperNoblium);
        var initBZ = mixture.GetMoles(Gas.BZ);
        var temperature = mixture.Temperature;
        float powerLoad = 100f;
        float activeLoad = (4200f * (3f * WorkingPower) * WorkingPower) / (PowerEfficiency + WorkingPower);
        var oldHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);

        if (initH2O > 0.05f)
        {
            var maxProportion = 2.5f * (float) Math.Pow(WorkingPower, 2);
            var proportion = Math.Min(initH2O * 0.5f, maxProportion);
            var temperatureEfficiency = Math.Min(mixture.Temperature / 1123.15f, 1f);

            var h2oRemoved = proportion * 2f;
            var oxyProduced = proportion * temperatureEfficiency;
            var hydrogenProduced = proportion * 2f * temperatureEfficiency;

            mixture.AdjustMoles(Gas.WaterVapor, -h2oRemoved);
            mixture.AdjustMoles(Gas.Oxygen, oxyProduced);
            mixture.AdjustMoles(Gas.Hydrogen, hydrogenProduced);

            var reactionPower = activeLoad * (hydrogenProduced / (maxProportion * 2f));
            powerLoad = Math.Max(reactionPower, powerLoad);
        }

        if (initHyperNob > 0.01f && temperature < 150f)
        {
            var maxProportion = 1.5f * (float) Math.Pow(WorkingPower, 2);
            var proportion = Math.Min(initHyperNob, maxProportion);
            mixture.AdjustMoles(Gas.HyperNoblium, -proportion);
            mixture.AdjustMoles(Gas.AntiNoblium, proportion * 0.5f);

            powerLoad = Math.Max(powerLoad, activeLoad * (proportion / maxProportion));
        }

        if (initBZ > 0.01f)
        {
            var proportion = Math.Min(initBZ * (1f - (float) Math.Pow(Math.E, -0.5f * temperature * WorkingPower / Atmospherics.FireMinimumTemperatureToExist)), initBZ);
            mixture.AdjustMoles(Gas.BZ, -proportion);
            mixture.AdjustMoles(Gas.Oxygen, proportion * 0.2f);
            mixture.AdjustMoles(Gas.Halon, proportion * 2f);
            var energyReleased = proportion * Atmospherics.HalonProductionEnergy;

            var newHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);
            powerLoad = Math.Max(powerLoad, activeLoad * Math.Min(proportion / 30f, 1));
        }

        var finalHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);
        if (finalHeatCapacity > Atmospherics.MinimumHeatCapacity && finalHeatCapacity != oldHeatCapacity)
            mixture.Temperature = Math.Max(mixture.Temperature * oldHeatCapacity / finalHeatCapacity, Atmospherics.TCMB);

        float fuelNeeded = powerLoad;

        electrolyzer.CurrentFuel = Math.Max(0f, electrolyzer.CurrentFuel - powerLoad);

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
        bool heldIsUranium = _tagSystem.HasTag(heldItem, UraniumTag);

        if (!heldIsPlasma && !heldIsUranium)
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
        bool existingIsUranium = _tagSystem.HasTag(existingItem.Value, UraniumTag);

        // Same type: merge
        if ((heldIsPlasma && existingIsPlasma) || (heldIsUranium && existingIsUranium))
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

        // Different type: swap
        EntityUid? ejected;
        if (_itemSlots.TryEject(uid, "fuel", args.User, out ejected))
        {
            // Insert the new held item first
            if (_itemSlots.TryInsert(uid, "fuel", heldItem, args.User))
            {
                _popup.PopupEntity(Loc.GetString("electrolyzer-fuel-swapped"), uid, args.User);

                if (ejected != EntityUid.Invalid && TryComp<HandsComponent>(args.User, out var hands))
                {
                    var activeHandId = hands.ActiveHandId;
                    if (activeHandId != null)
                    {
                        _handsSystem.TryPickup(args.User, ejected.Value, handId: activeHandId, handsComp: hands);
                    }
                    else
                    {
                        _handsSystem.PickupOrDrop(args.User, ejected.Value);
                    }
                }
            }
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
