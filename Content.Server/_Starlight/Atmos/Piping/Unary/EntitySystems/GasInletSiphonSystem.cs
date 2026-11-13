using Content.Server._Starlight.Atmos.Piping.Unary.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Atmos.Piping.Unary.Visuals;
using Content.Shared._Starlight.ToggleableDevice.Components;
using Content.Shared._Starlight.ToggleableDevice.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Power;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.Atmos.Piping.Unary.EntitySystems;

[UsedImplicitly]
public sealed partial class GasInletSiphonSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphereSystem = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private NodeContainerSystem _nodeContainer = null!;
    [Dependency] private PowerReceiverSystem _powerReceiverSystem = null!;
    [Dependency] private TransformSystem _transformSystem = null!;
    [Dependency] private AmbientSoundSystem _ambientSoundSystem = null!;
    [Dependency] private ToggleableDeviceSystem _toggleableDevice = null!;

    /// <summary>
    /// Ignore any persisted ToggleableDevice state, use our own.
    /// (Needed for backwards compat with existing mapped entities).
    /// </summary>
    [SubscribeLocalEvent]
    private void OnToggleableStatePreInit(Entity<GasInletSiphonComponent> entity, ref ToggleableDevicePreInitEvent args)
        => args.State = entity.Comp.Enabled;

    [SubscribeLocalEvent]
    private void OnToggleableStateInit(Entity<GasInletSiphonComponent> entity, ref ToggleableDeviceInitEvent args)
    {
        _toggleableDevice.SetLogType(entity.Owner, LogType.AtmosPowerChanged);
        UpdateAudioVisuals(entity);
    }

    /// <summary>
    /// On interact we delegate the toggling to the toggleable device system.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnActivate(Entity<GasInletSiphonComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        if (!TryComp<ToggleableDeviceComponent>(entity, out var toggleable))
            return;

        _toggleableDevice.Toggle((entity, toggleable), actor: args.User);
        args.Handled = true;
    }

    /// <summary>
    /// We update our state when the toggleable device system tells us state changed.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnStateChanged(Entity<GasInletSiphonComponent> entity, ref ToggleableDeviceStateChangedEvent args)
        => SetEnabled(entity, args.State);

    [SubscribeLocalEvent]
    private void OnGasInletSiphonEnterAtmosphere(Entity<GasInletSiphonComponent> entity, ref AtmosDeviceEnabledEvent args)
        => UpdateAudioVisuals(entity);

    [SubscribeLocalEvent]
    private void OnGasInletSiphonLeaveAtmosphere(Entity<GasInletSiphonComponent> entity, ref AtmosDeviceDisabledEvent args)
        => UpdateAudioVisuals(entity);

    [SubscribeLocalEvent]
    private void OnAirSiphonUpdated(Entity<GasInletSiphonComponent> entity, ref AtmosDeviceUpdateEvent args)
    {
        if (!_powerReceiverSystem.IsPowered(entity))
            return;

        var siphon = entity.Comp;

        if (!siphon.Enabled || !_nodeContainer.TryGetNode(entity.Owner, siphon.OutletName, out PipeNode? outlet))
            return;

        if (args.Grid is not {} grid)
            return;

        var position = _transformSystem.GetGridTilePositionOrDefault(entity.Owner);
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

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<GasInletSiphonComponent> entity, ref PowerChangedEvent args) => UpdateAudioVisuals(entity);

    private void UpdateAudioVisuals(Entity<GasInletSiphonComponent> entity, AppearanceComponent? appearance = null)
    {
        if (!Resolve(entity, ref appearance, false))
            return;

        var enabled = entity.Comp.Enabled;
        var powered = _powerReceiverSystem.IsPowered(entity);
        var state = (enabled ? 2 : 0) + (powered ? 1 : 0);
        _appearance.SetData(entity, GasInletSiphonVisuals.State, (GasInletSiphonState)state);
        _ambientSoundSystem.SetAmbience(entity, enabled && powered);
    }

    private void SetEnabled(Entity<GasInletSiphonComponent> siphon, bool value)
    {
        siphon.Comp.Enabled = value;
        UpdateAudioVisuals(siphon);
    }
}
