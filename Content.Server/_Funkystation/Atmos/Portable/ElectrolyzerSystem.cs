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
    private const float WorkingPower = 2f;
    private const float PowerEfficiency = 1f;
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
        if (!Transform(uid).Anchored || !electrolyzer.IsPowered)
            return;

        UpdateAppearance(uid);

        var mixture = _atmosphereSystem.GetContainingMixture(uid, args.Grid, args.Map);
        if (mixture is null) return;

        var Power = _battery.GetCharge(battery.Value.AsNullable());

        if (Power <= 0f)
        return;

        var fuelMultiplier = 1f;

        if (electrolyzer.CurrentFuel >= 0f)
        {
            fuelMultiplier = 10f;
        }

        var rate = Math.Min(1f, ((Power * fuelMultiplier)/500000f));

        var initH2O = mixture.GetMoles(Gas.WaterVapor);
        var initHyperNob = mixture.GetMoles(Gas.HyperNoblium);
        var initBZ = mixture.GetMoles(Gas.BZ);
        var temperature = mixture.Temperature;
        float powerLoad = 0f;
        float activeLoad = (4200f * (3f * WorkingPower) * WorkingPower) / (PowerEfficiency + WorkingPower);
        var oldHeatCapacity = _atmosphereSystem.GetHeatCapacity(mixture, true);

        if (initH2O > 0.05f)
        {
            var maxProportion = 0.25f * rate * (float) Math.Pow(WorkingPower, 2);
            var proportion = Math.Min(initH2O * 0.5f, maxProportion);
            var temperatureEfficiency = Math.Min(mixture.Temperature / 1123.15f, 1f);

            var h2oRemoved = proportion * 2f;
            var oxyProduced = proportion * temperatureEfficiency;
            var hydrogenProduced = proportion * 2f * temperatureEfficiency;

            mixture.AdjustMoles(Gas.WaterVapor, -h2oRemoved * electrolyzer.Efficiency);
            mixture.AdjustMoles(Gas.Oxygen, oxyProduced * electrolyzer.Efficiency);
            mixture.AdjustMoles(Gas.Hydrogen, hydrogenProduced * electrolyzer.Efficiency);

            var reactionPower = activeLoad * (hydrogenProduced / (maxProportion * 2f));
            powerLoad = Math.Max(reactionPower, powerLoad);
        }

        if (initHyperNob > 0.01f && temperature < 150f)
        {
            var maxProportion = 0.15f * rate * (float) Math.Pow(WorkingPower, 2);
            var proportion = Math.Min(initHyperNob, maxProportion * electrolyzer.Efficiency);
            mixture.AdjustMoles(Gas.HyperNoblium, -proportion * electrolyzer.Efficiency);
            mixture.AdjustMoles(Gas.AntiNoblium, proportion * 0.5f * electrolyzer.Efficiency);

            powerLoad = Math.Max(powerLoad, activeLoad * (proportion / maxProportion));
        }

        if (initBZ > 0.01f)
        {
            var proportion = Math.Min(initBZ * rate * (0.1f - (float) Math.Pow(Math.E, -0.5f * temperature * WorkingPower / Atmospherics.FireMinimumTemperatureToExist)), initBZ);
            mixture.AdjustMoles(Gas.BZ, -proportion * electrolyzer.Efficiency);
            mixture.AdjustMoles(Gas.Oxygen, proportion * 0.2f * electrolyzer.Efficiency);
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

       _battery..ChangeCharge(battery.Value.AsNullable(), -50000f);
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
