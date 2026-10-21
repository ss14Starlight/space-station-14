using System.Diagnostics.CodeAnalysis;
using Content.Server._Starlight.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._Starlight.GasMining.GasExtractor;
using Content.Shared._Starlight.GasMining.GasNode;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Starlight.GasMining.GasExtractor;

public sealed partial class GasExtractorSystem : SharedGasExtractorSystem
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private AmbientSoundSystem _ambientSoundSystem = default!;
    [Dependency] private NodeContainerSystem _nodeContainer = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private MapSystem _mapSystem = default!;
    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private SharedGasNodeSystem _sharedGasNodeSystem = default!;

    [SubscribeLocalEvent]
    private void OnActivate(Entity<GasExtractorComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
    }

    [SubscribeLocalEvent]
    private void OnSetToggleSignalReceived(Entity<ToggleableAtmosDeviceComponent> entity, ref SetToggleSignalReceivedEvent args)
    {
        if (!_entityManager.TryGetComponent<GasExtractorComponent>(entity, out var device))
            return;

        var extractorEntity = new Entity<GasExtractorComponent>(entity.Owner, device);
        Set(extractorEntity, args.Value);
    }

    [SubscribeLocalEvent]
    private void OnToggleSignalReceived(Entity<ToggleableAtmosDeviceComponent> entity, ref ToggleSignalReceivedEvent args)
    {
        if (!_entityManager.TryGetComponent<GasExtractorComponent>(entity, out var device))
            return;

        var extractorEntity = new Entity<GasExtractorComponent>(entity.Owner, device);
        Set(extractorEntity, !device.Enabled);
    }

    [SubscribeLocalEvent]
    private void OnGasExtractorUpdated(Entity<GasExtractorComponent> entity, ref AtmosDeviceUpdateEvent args)
    {
        var extractor = entity.Comp;

        if (!extractor.Enabled || !_nodeContainer.TryGetNode(entity.Owner, extractor.OutletName, out PipeNode? outlet))
            return;

        if (args.Grid is not {} grid)
            return;

        if (!Transform(entity.Owner).Anchored)
            return;

        if (!GetGasNode(entity, out var gasNode))
            return;

        ExtractGas(entity, gasNode, args.dt);
    }

    private void ExtractGas(Entity<GasExtractorComponent> extractor, Entity<GasNodeComponent> gasNode, float deltaTime)
    {
        if (!extractor.Comp.Enabled || !_nodeContainer.TryGetNode(extractor.Owner, extractor.Comp.OutletName, out PipeNode? gasPipe)
            || gasPipe.Air.Pressure >= extractor.Comp.MaxBufferPressure
            || gasNode.Comp.Gases.Count <= 0
            )
            return;

        var transferMoles = extractor.Comp.MolesExtractedPerSecond * deltaTime;
        if (transferMoles <= 0)
            return;

        var extracted = _sharedGasNodeSystem.TransferGas(gasNode, transferMoles);

        var extractedGasMixture = extracted.Clone();

        var availableMoles = extractedGasMixture.TotalMoles;
        var limitMolesExtracted = AtmosphereSystem.MolesToMaxPressure(extractedGasMixture, gasPipe.Air, Atmospherics.MaxOutputPressure * 2);

        var extractedMoles = Math.Clamp(limitMolesExtracted, 0f, availableMoles);
        var extractRatio = availableMoles > 0f ? extractedMoles / availableMoles : 0f;

        extractedGasMixture.Multiply(extractRatio);
        foreach (var (gas, moles) in extractedGasMixture)
            extracted.AdjustMoles(gas, -moles);

        _atmosphereSystem.Merge(gasPipe.Air, extractedGasMixture);

        if (extracted.TotalMoles > 0f)
        {
            _sharedGasNodeSystem.ReturnGas(gasNode, extracted);
        }
    }

    private bool GetGasNode(Entity<GasExtractorComponent> entity, [NotNullWhen(true)] out Entity<GasNodeComponent> gasNode)
    {
        var xform = Transform(entity);
        gasNode = default;

        if (xform.GridUid == null)
            return false;

        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return false;

        var tile = _mapSystem.TileIndicesFor(
            xform.GridUid.Value,
            grid,
            xform.Coordinates
        );

        foreach (var ent in  _mapSystem.GetAnchoredEntities(
            xform.GridUid.Value,
            grid,
            tile
        ))
        {
            if (ent == entity.Owner)
                continue;

            if (_entityManager.TryGetComponent<GasNodeComponent>(ent, out var gasNodeComponent))
                {
                    gasNode = new Entity<GasNodeComponent>(ent, gasNodeComponent);
                    return true;
                }
        }

        return false;
    }

    private void UpdateState(Entity<GasExtractorComponent> entity, AppearanceComponent? appearance = null)
    {
        var extractor = entity.Comp;

        if (!Resolve(entity.Owner, ref appearance, false))
            return;

        var enabled = extractor.Enabled;
    }

    private void Set(Entity<GasExtractorComponent> entity, bool value)
    {
        if (entity.Comp.Enabled == value)
            return;

        entity.Comp.Enabled = value;
        UpdateState(entity);
    }
}
