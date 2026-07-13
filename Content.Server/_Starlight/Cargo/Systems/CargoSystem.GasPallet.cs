using Content.Server._Starlight.Cargo.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;

namespace Content.Server._Starlight.Cargo.Systems;

/// <summary>
/// A variant of the ATS cargo pallets that deals with gasses
/// fed through pipe systems instead of in canisters, allowing
/// for high-volume buying and selling.
/// </summary>
public sealed partial class CargoGasPalletSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private NodeContainerSystem _nodeContainer = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CargoGasPalletComponent, AtmosDeviceUpdateEvent>(OnAtmosDeviceUpdateEvent);
        SubscribeLocalEvent<EntitySellContentsEvent>(OnEntitySellContentsEvent);
        SubscribeLocalEvent<CargoGasPalletComponent, GasAnalyzerScanEvent>(OnAnalyzed);
    }

    /// <summary>
    /// Report the internal buffer to the gas analyzer, since it isn't a pipe node
    /// </summary>
    private void OnAnalyzed(EntityUid uid, CargoGasPalletComponent pallet, GasAnalyzerScanEvent args)
    {
        args.GasMixtures ??= new List<(string, GasMixture?)>();

        // Display gas in the buffer, separate from our pipenet connection
        args.GasMixtures.Add(("Buffer", pallet.Air));

        // Separately show what is in this piece of pipenet.
        if (_nodeContainer.TryGetNode(uid, pallet.InletName, out PipeNode? inlet) && inlet.Air.Volume > 0f)
        {
            var pipeAir = inlet.Air.Clone();
            pipeAir.Multiply(inlet.Volume / inlet.Air.Volume);
            pipeAir.Volume = inlet.Volume;
            args.GasMixtures.Add((inlet.Name, pipeAir));
        }
    }

    private void OnAtmosDeviceUpdateEvent(EntityUid uid, CargoGasPalletComponent pallet, ref AtmosDeviceUpdateEvent args)
    {
        if (!_nodeContainer.TryGetNode(uid, pallet.InletName, out PipeNode? inlet)) return;

        if ((pallet.PalletType & BuySellType.Sell) != 0)
            SellTransfer(inlet, pallet);

        if ((pallet.PalletType & BuySellType.Buy) != 0)
            BuyTransfer(inlet, pallet);
    }

    /// <summary>
    /// Handle gas movement into the internal pre-sale resivoir of the CargoGasPallet
    /// </summary>
    private void SellTransfer(PipeNode inlet, CargoGasPalletComponent pallet)
    {
        var outputStartingPressure = pallet.Air.Pressure;

        if (outputStartingPressure >= pallet.MaxPressure)
        {
            return;
        }

        // Vent into a large but finite internal buffer
        if (inlet.Air.TotalMoles > 0 && inlet.Air.Pressure > 0)
        {
            var pressureDelta = pallet.MaxPressure - outputStartingPressure;
            var transferMoles = pressureDelta * pallet.Air.Volume / (inlet.Air.Temperature * Atmospherics.R);
            var removed = inlet.Air.Remove(transferMoles);
            _atmosphereSystem.Merge(pallet.Air, removed);
        }
    }

    /// <summary>
    /// Handle gas movement out of the internal post-purchase resivoir of the CargoGasPallet, into the pipenet
    /// </summary>
    private void BuyTransfer(PipeNode inlet, CargoGasPalletComponent pallet)
    {
        var inletStartingPressure = inlet.Air.Pressure;

        if (inletStartingPressure >= pallet.MaxPressure)
        {
            return;
        }

        // Dispense purchased gas from the internal buffer into the pipenet
        if (pallet.Air.TotalMoles > 0 && pallet.Air.Pressure > 0)
        {
            var pressureDelta = pallet.MaxPressure - inletStartingPressure;
            var transferMoles = pressureDelta * inlet.Air.Volume / (pallet.Air.Temperature * Atmospherics.R);
            var removed = pallet.Air.Remove(transferMoles);
            _atmosphereSystem.Merge(inlet.Air, removed);
        }
    }

    /// <summary>
    /// Handle clearing the internal resivoir when we sell the gas, so we can't sell it twice
    /// </summary>
    private void OnEntitySellContentsEvent(ref EntitySellContentsEvent args) {
        foreach (var pallet in args.Containers) {
            if ((pallet.PalletType & BuySellType.Sell) != 0)
                pallet.Air.Clear();
        }
    }
}
