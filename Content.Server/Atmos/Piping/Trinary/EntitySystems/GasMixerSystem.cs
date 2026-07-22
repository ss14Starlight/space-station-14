using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Trinary.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Atmos.Piping.Trinary.Components;
using Content.Shared.Audio;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Atmos.Piping.Trinary.EntitySystems
{
    [UsedImplicitly]
    public sealed partial class GasMixerSystem : EntitySystem
    {
        [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private IAdminLogManager _adminLogger = default!;
        [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private SharedAmbientSoundSystem _ambientSoundSystem = default!;
        [Dependency] private SharedAppearanceSystem _appearance = default!;
        [Dependency] private NodeContainerSystem _nodeContainer = default!;
        [Dependency] private SharedPopupSystem _popup = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GasMixerComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<GasMixerComponent, AtmosDeviceUpdateEvent>(OnMixerUpdated);
            SubscribeLocalEvent<GasMixerComponent, ActivateInWorldEvent>(OnMixerActivate);
            SubscribeLocalEvent<GasMixerComponent, GasAnalyzerScanEvent>(OnMixerAnalyzed);
            SubscribeLocalEvent<GasMixerComponent, AnchorStateChangedEvent>(OnAnchorChanged); // Starlight
            // Bound UI subscriptions
            SubscribeLocalEvent<GasMixerComponent, GasMixerChangeOutputPressureMessage>(OnOutputPressureChangeMessage);
            SubscribeLocalEvent<GasMixerComponent, GasMixerChangeNodePercentageMessage>(OnChangeNodePercentageMessage);
            SubscribeLocalEvent<GasMixerComponent, GasMixerToggleStatusMessage>(OnToggleStatusMessage);

            SubscribeLocalEvent<GasMixerComponent, AtmosDeviceDisabledEvent>(OnMixerLeaveAtmosphere);
        }

        private void OnInit(EntityUid uid, GasMixerComponent mixer, ComponentInit args)
        {
            UpdateAppearance(uid, core: FilterPortVisualsState.SolidOrange);
        }

        private void OnMixerUpdated(EntityUid uid, GasMixerComponent mixer, ref AtmosDeviceUpdateEvent args)
        {
            DoUpdate(uid, mixer, out var core, out var inlet, out var side, out var outlet);
            UpdateAppearanceDelta(uid, core: core, inlet: inlet, side: side, outlet: outlet);
        }

        private void DoUpdate(EntityUid uid, GasMixerComponent mixer,
            out FilterPortVisualsState? coreVisual,
            out FilterPortVisualsState? inletVisual,
            out FilterPortVisualsState? sideVisual,
            out FilterPortVisualsState? outletVisual)
        {
            coreVisual = null;
            inletVisual = null;
            sideVisual = null;
            outletVisual = null;

            // TODO ATMOS: Cache total moles since it's expensive.

            if (!mixer.Enabled
                || !_nodeContainer.TryGetNodes(uid, mixer.InletOneName, mixer.InletTwoName, mixer.OutletName,
                    out PipeNode? inletOne, out PipeNode? inletTwo, out PipeNode? outlet))
            {
                _ambientSoundSystem.SetAmbience(uid, false);
                return;
            }

            inletVisual = FilterPortVisualsState.Off;
            sideVisual = FilterPortVisualsState.Off;
            coreVisual = FilterPortVisualsState.Off;
            outletVisual = FilterPortVisualsState.Off;

            var outputStartingPressure = outlet.Air.Pressure;

            if (outputStartingPressure >= mixer.TargetPressure)
            {
                coreVisual = FilterPortVisualsState.SolidYellow;
                outletVisual = FilterPortVisualsState.SolidYellow;
                return; // Target reached, no need to mix.
            }

            var generalTransfer = (mixer.TargetPressure - outputStartingPressure) * outlet.Air.Volume / Atmospherics.R;

            var transferMolesOne = inletOne.Air.Temperature > 0 ? mixer.InletOneConcentration * generalTransfer / inletOne.Air.Temperature : 0f;
            var transferMolesTwo = inletTwo.Air.Temperature > 0 ? mixer.InletTwoConcentration * generalTransfer / inletTwo.Air.Temperature : 0f;

            if (mixer.InletTwoConcentration <= 0f)
            {
                transferMolesOne = MathF.Min(transferMolesOne, inletOne.Air.TotalMoles);
                transferMolesTwo = 0f;

                if (inletOne.Air.Temperature <= 0f || transferMolesOne <= 0f)
                {
                    coreVisual = FilterPortVisualsState.SolidRed;
                    inletVisual = FilterPortVisualsState.BlinkingRed;
                    return;
                }
            }

            else if (mixer.InletOneConcentration <= 0)
            {
                transferMolesOne = 0f;
                transferMolesTwo = MathF.Min(transferMolesTwo, inletTwo.Air.TotalMoles);

                if (inletTwo.Air.Temperature <= 0f || transferMolesTwo <= 0f)
                {
                    coreVisual = FilterPortVisualsState.SolidRed;
                    sideVisual = FilterPortVisualsState.BlinkingRed;
                    return;
                }
            }
            else
            {
                if (inletOne.Air.Temperature <= 0f)
                {
                    coreVisual = FilterPortVisualsState.SolidRed;
                    inletVisual = FilterPortVisualsState.BlinkingRed;
                    return;
                }

                if (inletTwo.Air.Temperature <= 0f)
                {
                    coreVisual = FilterPortVisualsState.SolidRed;
                    sideVisual = FilterPortVisualsState.BlinkingRed;
                    return;
                }

                if (transferMolesOne <= 0 || transferMolesTwo <= 0)
                {
                    _ambientSoundSystem.SetAmbience(uid, false);
                    return;
                }

                if (inletOne.Air.TotalMoles < transferMolesOne || inletTwo.Air.TotalMoles < transferMolesTwo)
                {
                    var inletOneRatio = inletOne.Air.TotalMoles / transferMolesOne;
                    var inletTwoRatio = inletTwo.Air.TotalMoles / transferMolesTwo;
                    outletVisual = FilterPortVisualsState.Off;

                    if (inletOneRatio <= 0f)
                    {
                        inletVisual = FilterPortVisualsState.BlinkingRed;
                        coreVisual = FilterPortVisualsState.SolidRed;
                    }

                    if (inletTwoRatio <= 0f)
                    {
                        sideVisual = FilterPortVisualsState.BlinkingRed;
                        coreVisual = FilterPortVisualsState.SolidRed;
                    }

                    var ratio = MathF.Min(inletOneRatio, inletTwoRatio);
                    transferMolesOne *= ratio;
                    transferMolesTwo *= ratio;
                }
            }

            // Actually transfer the gas now.
            var transferred = false;

            if (transferMolesOne > 0f)
            {
                transferred = true;
                var removed = inletOne.Air.Remove(transferMolesOne);
                _atmosphereSystem.Merge(outlet.Air, removed);
            }

            if (transferMolesTwo > 0f)
            {
                transferred = true;
                var removed = inletTwo.Air.Remove(transferMolesTwo);
                _atmosphereSystem.Merge(outlet.Air, removed);
            }

            if (transferred)
            {
                coreVisual = FilterPortVisualsState.SolidGreen;
                outletVisual = FilterPortVisualsState.SolidGreen;
                _ambientSoundSystem.SetAmbience(uid, true);
            }
        }

        // Starlight Start
        private void OnAnchorChanged(EntityUid uid, GasMixerComponent mixer, ref AnchorStateChangedEvent args)
        {
            if (!args.Anchored)
            {
                mixer.Enabled = false;
                DirtyUI(uid, mixer);
                UpdateAppearance(uid);
                return;
            }

            UpdateAppearance(uid, core: FilterPortVisualsState.SolidOrange);
        }
        // Starlight End

        private void OnMixerLeaveAtmosphere(EntityUid uid, GasMixerComponent mixer, ref AtmosDeviceDisabledEvent args)
        {
            // mixer.Enabled = false; // Starlight Edit: Moved to OnAnchorChanged

            DirtyUI(uid, mixer);
            UpdateAppearance(uid);
            _userInterfaceSystem.CloseUi(uid, GasFilterUiKey.Key);
        }

        private void OnMixerActivate(EntityUid uid, GasMixerComponent mixer, ActivateInWorldEvent args)
        {
            if (args.Handled || !args.Complex)
                return;

            if (!TryComp(args.User, out ActorComponent? actor))
                return;

            if (Transform(uid).Anchored)
            {
                _userInterfaceSystem.OpenUi(uid, GasMixerUiKey.Key, actor.PlayerSession);
                DirtyUI(uid, mixer);
            }
            else
            {
                _popup.PopupCursor(Loc.GetString("comp-gas-mixer-ui-needs-anchor"), args.User);
            }

            args.Handled = true;
        }

        private void DirtyUI(EntityUid uid, GasMixerComponent? mixer)
        {
            if (!Resolve(uid, ref mixer))
                return;

            _userInterfaceSystem.SetUiState(uid, GasMixerUiKey.Key,
                new GasMixerBoundUserInterfaceState(Comp<MetaDataComponent>(uid).EntityName, mixer.TargetPressure,
                    mixer.Enabled, mixer.InletOneConcentration));
        }

        private void UpdateAppearance(EntityUid uid,
            AppearanceComponent? appearance = null,
            FilterPortVisualsState core = FilterPortVisualsState.Off,
            FilterPortVisualsState inlet = FilterPortVisualsState.Off,
            FilterPortVisualsState outlet = FilterPortVisualsState.Off,
            FilterPortVisualsState side = FilterPortVisualsState.Off) =>
            UpdateAppearanceDelta(uid, appearance, core, inlet, outlet, side);

        private void UpdateAppearanceDelta(EntityUid uid,
            AppearanceComponent? appearance = null,
            FilterPortVisualsState? core = null,
            FilterPortVisualsState? inlet = null,
            FilterPortVisualsState? outlet = null,
            FilterPortVisualsState? side = null)
        {
            if (!Resolve(uid, ref appearance, false))
                return;

            if (core != null)
                _appearance.SetData(uid, FilterVisuals.Core, core, appearance);
            if (inlet != null)
                _appearance.SetData(uid, FilterVisuals.Inlet, inlet, appearance);
            if (outlet != null)
                _appearance.SetData(uid, FilterVisuals.Outlet, outlet, appearance);
            if (side != null)
                _appearance.SetData(uid, FilterVisuals.Side, side, appearance);
            // Dirty(uid, appearance);
        }

        private void OnToggleStatusMessage(EntityUid uid, GasMixerComponent mixer, GasMixerToggleStatusMessage args)
        {
            mixer.Enabled = args.Enabled;
            _adminLogger.Add(LogType.AtmosPowerChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} set the power on {ToPrettyString(uid):device} to {args.Enabled}");
            DirtyUI(uid, mixer);
            if (!mixer.Enabled)
                UpdateAppearance(uid, core: FilterPortVisualsState.SolidOrange);
        }

        private void OnOutputPressureChangeMessage(EntityUid uid, GasMixerComponent mixer,
            GasMixerChangeOutputPressureMessage args)
        {
            mixer.TargetPressure = Math.Clamp(args.Pressure, 0f, mixer.MaxTargetPressure);
            _adminLogger.Add(LogType.AtmosPressureChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} set the pressure on {ToPrettyString(uid):device} to {args.Pressure}kPa");
            DirtyUI(uid, mixer);
        }

        private void OnChangeNodePercentageMessage(EntityUid uid, GasMixerComponent mixer,
            GasMixerChangeNodePercentageMessage args)
        {
            float nodeOne = Math.Clamp(args.NodeOne, 0f, 100.0f) / 100.0f;
            mixer.InletOneConcentration = nodeOne;
            mixer.InletTwoConcentration = 1.0f - mixer.InletOneConcentration;
            _adminLogger.Add(LogType.AtmosRatioChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} set the ratio on {ToPrettyString(uid):device} to {mixer.InletOneConcentration}:{mixer.InletTwoConcentration}");
            DirtyUI(uid, mixer);
        }

        /// <summary>
        /// Returns the gas mixture for the gas analyzer
        /// </summary>
        private void OnMixerAnalyzed(EntityUid uid, GasMixerComponent component, GasAnalyzerScanEvent args)
        {
            args.GasMixtures ??= new List<(string, GasMixture?)>();

            // multiply by volume fraction to make sure to send only the gas inside the analyzed pipe element, not the whole pipe system
            if (_nodeContainer.TryGetNode(uid, component.InletOneName, out PipeNode? inletOne) &&
                inletOne.Air.Volume != 0f)
            {
                var inletOneAirLocal = inletOne.Air.Clone();
                inletOneAirLocal.Multiply(inletOne.Volume / inletOne.Air.Volume);
                inletOneAirLocal.Volume = inletOne.Volume;
                args.GasMixtures.Add((
                    $"{inletOne.CurrentPipeDirection} {Loc.GetString("gas-analyzer-window-text-inlet")}",
                    inletOneAirLocal));
            }

            if (_nodeContainer.TryGetNode(uid, component.InletTwoName, out PipeNode? inletTwo) &&
                inletTwo.Air.Volume != 0f)
            {
                var inletTwoAirLocal = inletTwo.Air.Clone();
                inletTwoAirLocal.Multiply(inletTwo.Volume / inletTwo.Air.Volume);
                inletTwoAirLocal.Volume = inletTwo.Volume;
                args.GasMixtures.Add((
                    $"{inletTwo.CurrentPipeDirection} {Loc.GetString("gas-analyzer-window-text-inlet")}",
                    inletTwoAirLocal));
            }

            if (_nodeContainer.TryGetNode(uid, component.OutletName, out PipeNode? outlet) && outlet.Air.Volume != 0f)
            {
                var outletAirLocal = outlet.Air.Clone();
                outletAirLocal.Multiply(outlet.Volume / outlet.Air.Volume);
                outletAirLocal.Volume = outlet.Volume;
                args.GasMixtures.Add((Loc.GetString("gas-analyzer-window-text-outlet"), outletAirLocal));
            }

            args.DeviceFlipped = inletOne != null && inletTwo != null && inletOne.CurrentPipeDirection.ToDirection() ==
                inletTwo.CurrentPipeDirection.ToDirection().GetClockwise90Degrees();
        }
    }
}
