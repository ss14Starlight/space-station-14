using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Cargo.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;

namespace Content.Server.Cargo.Systems;

/// <summary>
/// A variant of the ATS cargo pallets that deals with gasses
/// fed through pipe systems instead of in canisters, allowing
/// for high-volume sales.
/// </summary>
public sealed class CargoGasPalletSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CargoGasPalletComponent, AtmosDeviceUpdateEvent>(OnAtmosDeviceUpdateEvent);
        SubscribeLocalEvent<EntitySellContentsEvent>(OnEntitySellContentsEvent);
    }

    /// <summary>
    /// Handle gas movement into the internal pre-sale resivoir of the CargoGasPallet
    /// </summary>
    private void OnAtmosDeviceUpdateEvent(EntityUid uid, CargoGasPalletComponent pallet, ref AtmosDeviceUpdateEvent args)
    {
        if (!_nodeContainer.TryGetNode(uid, pallet.InletName, out PipeNode? inlet))
        {
            return;
        }

        var outputStartingPressure = pallet.Air.Pressure;

        if (outputStartingPressure >= pallet.MaxPressure)
        {
            return;
        }

        // Vent into a large but finite internal buffer
        if (inlet.Air.TotalMoles > 0 && inlet.Air.Pressure > 0)
        {
            var pressureDelta = pallet.MaxPressure - outputStartingPressure;
            var transferMoles = (pressureDelta * pallet.Air.Volume) / (inlet.Air.Temperature * Atmospherics.R);
            var removed = inlet.Air.Remove(transferMoles);
            _atmosphereSystem.Merge(pallet.Air, removed);
        }
    }

    /// <summary>
    /// Handle clearing the internal resivoir when we sell the gas, so we can't sell it twice
    /// </summary>
    private void OnEntitySellContentsEvent(ref EntitySellContentsEvent args) {
        foreach (var pallet in args.Containers) {
            pallet.Air.Clear();
        }
    }
}
