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
        if (!_nodeContainer.TryGetNode(uid, pallet.PipeNodeName, out PipeNode? inlet) ||
            !(inlet.Air.Volume > 0f)) return;
        var pipeAir = inlet.Air.Clone();
        pipeAir.Multiply(inlet.Volume / inlet.Air.Volume);
        pipeAir.Volume = inlet.Volume;
        args.GasMixtures.Add((inlet.Name, pipeAir));
    }

    private void OnAtmosDeviceUpdateEvent(EntityUid uid, CargoGasPalletComponent pallet, ref AtmosDeviceUpdateEvent args)
    {
        if (!_nodeContainer.TryGetNode(uid, pallet.PipeNodeName, out PipeNode? inlet)) return;

        if ((pallet.PalletType & BuySellType.Sell) != 0)
            UpdateIngress(inlet, pallet);

        if ((pallet.PalletType & BuySellType.Buy) != 0)
            UpdateEgress(inlet, pallet);
    }

    /// <summary>
    /// Transfer gas from pipenet => buffer.
    /// </summary>
    private void UpdateIngress(PipeNode node, CargoGasPalletComponent pallet) =>
        TransferGas(node.Air, pallet.Air, pallet.MaxPressure);

    /// <summary>
    /// Transfer gas from buffer => pipenet.
    /// </summary>
    private void UpdateEgress(PipeNode node, CargoGasPalletComponent pallet) =>
        TransferGas(pallet.Air, node.Air, pallet.MaxPressure);

    /// <summary>
    /// Moves gas from <paramref name="source"/> into <paramref name="dest"/> until
    /// <paramref name="dest"/> reaches <paramref name="maxPressure"/>.
    /// </summary>
    private void TransferGas(GasMixture source, GasMixture dest, float maxPressure)
    {
        var destPressure = dest.Pressure;
        if (destPressure >= maxPressure) return;

        if (source is not { TotalMoles: > 0, Pressure: > 0 }) return;

        var pressureDelta = maxPressure - destPressure;
        var transferMoles = pressureDelta * dest.Volume / (source.Temperature * Atmospherics.R);
        var removed = source.Remove(transferMoles);
        _atmosphereSystem.Merge(dest, removed);
    }

    /// <summary>
    /// Handle clearing the internal reservoir when we sell the gas, so we can't sell it twice
    /// </summary>
    private void OnEntitySellContentsEvent(ref EntitySellContentsEvent args)
    {
        foreach (var pallet in args.Containers)
            if ((pallet.PalletType & BuySellType.Sell) != 0)
                pallet.Air.Clear();
    }
}
