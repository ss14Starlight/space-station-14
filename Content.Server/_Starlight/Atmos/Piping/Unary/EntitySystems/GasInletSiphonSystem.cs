using Content.Server._Starlight.Atmos.Piping.Unary.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Audio;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Atmos.Piping.Unary.Visuals;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Interaction;
using Content.Shared.Power;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.Atmos.Piping.Unary.EntitySystems;

[UsedImplicitly]
public sealed partial class GasInletSiphonSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private NodeContainerSystem _nodeContainer = default!;
    [Dependency] private PowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private AmbientSoundSystem _ambientSoundSystem = default!;
    [Dependency] private EntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasInletSiphonComponent, AtmosDeviceUpdateEvent>(OnAirSiphonUpdated);
        SubscribeLocalEvent<GasInletSiphonComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<GasInletSiphonComponent, AtmosDeviceEnabledEvent>(OnGasInletSiphonEnterAtmosphere);
        SubscribeLocalEvent<GasInletSiphonComponent, AtmosDeviceDisabledEvent>(OnGasInletSiphonLeaveAtmosphere);
        SubscribeLocalEvent<GasInletSiphonComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GasInletSiphonComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ToggleableAtmosDeviceComponent, SetToggleSignalReceivedEvent>(OnSetToggleSignalReceived);
        SubscribeLocalEvent<ToggleableAtmosDeviceComponent, ToggleSignalReceivedEvent>(OnToggleSignalReceived);
    }

    private void OnMapInit(EntityUid uid, GasInletSiphonComponent component, MapInitEvent args) => UpdateState(uid, component);

    private void OnActivate(EntityUid uid, GasInletSiphonComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        Set(uid, component, !component.Enabled);
        UpdateState(uid, component);
        args.Handled = true;
    }

    private void OnSetToggleSignalReceived(EntityUid uid, ToggleableAtmosDeviceComponent component, ref SetToggleSignalReceivedEvent args)
    {
        if (!_entityManager.TryGetComponent<GasInletSiphonComponent>(uid, out var device))
            return;
        Set(uid, device, args.Value);
    }

    private void OnToggleSignalReceived(EntityUid uid, ToggleableAtmosDeviceComponent component, ref ToggleSignalReceivedEvent args)
    {
        if (!_entityManager.TryGetComponent<GasInletSiphonComponent>(uid, out var device))
            return;

        Set(uid, device, !device.Enabled);
    }

    private void OnGasInletSiphonEnterAtmosphere(EntityUid uid, GasInletSiphonComponent component, ref AtmosDeviceEnabledEvent args) => UpdateState(uid, component);

    private void OnGasInletSiphonLeaveAtmosphere(EntityUid uid, GasInletSiphonComponent component, ref AtmosDeviceDisabledEvent args) => UpdateState(uid, component);

    private void OnAirSiphonUpdated(EntityUid uid, GasInletSiphonComponent siphon, ref AtmosDeviceUpdateEvent args)
    {
        if (!_powerReceiverSystem.IsPowered(uid))
            return;

        if (!siphon.Enabled || !_nodeContainer.TryGetNode(uid, siphon.OutletName, out PipeNode? outlet))
            return;

        if (args.Grid is not {} grid)
            return;

        var position = _transformSystem.GetGridTilePositionOrDefault(uid);
        var environment = _atmosphereSystem.GetTileMixture(grid, args.Map, position, true);

        Scrub(args.dt, siphon, environment, outlet.Air);

        var enumerator = _atmosphereSystem.GetAdjacentTileMixtures(grid, position, false, true);
        while (enumerator.MoveNext(out var adjacent))
        {
            Scrub(args.dt, siphon, adjacent, outlet.Air);
        }
    }

    private void Scrub(float deltaTime, GasInletSiphonComponent siphon, GasMixture? tile, GasMixture destination)
    {
        if (tile == null
            || destination.Pressure >= siphon.MaxPressure)
            return;

        var ratio = MathF.Min(1f, deltaTime * siphon.TransferRate*_atmosphereSystem.PumpSpeedup() / tile.Volume);
        var removed = tile.RemoveRatio(ratio);

        if (MathHelper.CloseToPercent(removed.TotalMoles, 0f))
            return;

        _atmosphereSystem.Merge(destination, removed);
    }

    private void OnPowerChanged(EntityUid uid, GasInletSiphonComponent component, ref PowerChangedEvent args) => UpdateState(uid, component);

    private void UpdateState(EntityUid uid, GasInletSiphonComponent siphon, AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref appearance, false))
            return;

        _ambientSoundSystem.SetAmbience(uid, true);

        if (!_powerReceiverSystem.IsPowered(uid) && !siphon.Enabled) {
            _ambientSoundSystem.SetAmbience(uid, false);
            _appearance.SetData(uid, GasInletSiphonVisuals.State, GasInletSiphonState.UnpoweredOff, appearance);
        } else if (!_powerReceiverSystem.IsPowered(uid) && siphon.Enabled)
        {
            _ambientSoundSystem.SetAmbience(uid, false);
            _appearance.SetData(uid, GasInletSiphonVisuals.State, GasInletSiphonState.UnpoweredOn, appearance);
        } else if (_powerReceiverSystem.IsPowered(uid) && !siphon.Enabled)
        {
            _ambientSoundSystem.SetAmbience(uid, false);
            _appearance.SetData(uid, GasInletSiphonVisuals.State, GasInletSiphonState.Off, appearance);
        } else if (_powerReceiverSystem.IsPowered(uid) && siphon.Enabled)
        {
            _appearance.SetData(uid, GasInletSiphonVisuals.State, GasInletSiphonState.On, appearance);
        }
    }

    private void Set(EntityUid uid, GasInletSiphonComponent siphon, bool value)
    {
        if (siphon.Enabled == value)
            return;

        siphon.Enabled = value;
        UpdateState(uid, siphon);
    }
}
