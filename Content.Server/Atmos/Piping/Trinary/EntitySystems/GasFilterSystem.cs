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
    public sealed partial class GasFilterSystem : EntitySystem
    {
        [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private IAdminLogManager _adminLogger = default!;
        [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private SharedAmbientSoundSystem _ambientSoundSystem = default!;
        [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
        [Dependency] private SharedPopupSystem _popupSystem = default!;
        [Dependency] private NodeContainerSystem _nodeContainer = default!;

        private const float DeltaMolCutoff = 0.001f;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GasFilterComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<GasFilterComponent, AtmosDeviceUpdateEvent>(OnFilterUpdated);
            SubscribeLocalEvent<GasFilterComponent, AtmosDeviceDisabledEvent>(OnFilterLeaveAtmosphere);
            SubscribeLocalEvent<GasFilterComponent, ActivateInWorldEvent>(OnFilterActivate);
            SubscribeLocalEvent<GasFilterComponent, GasAnalyzerScanEvent>(OnFilterAnalyzed);
            SubscribeLocalEvent<GasFilterComponent, AnchorStateChangedEvent>(OnAnchorChanged); // Starlight
            // Bound UI subscriptions
            SubscribeLocalEvent<GasFilterComponent, GasFilterChangeRateMessage>(OnTransferRateChangeMessage);
            SubscribeLocalEvent<GasFilterComponent, GasFilterAddGasMessage>(OnAddGasMessage);
            SubscribeLocalEvent<GasFilterComponent, GasFilterRemoveGasMessage>(OnRemoveGasMessage);
            SubscribeLocalEvent<GasFilterComponent, GasFilterToggleStatusMessage>(OnToggleStatusMessage);

        }

        private void OnInit(EntityUid uid, GasFilterComponent filter, ComponentInit args)
        {
            UpdateAppearance(uid, filter);
        }

        private void OnFilterUpdated(EntityUid uid, GasFilterComponent filter, ref AtmosDeviceUpdateEvent args)
        {
            DoFilterUpdated(uid, filter, ref args,
                out var core, out var inlet, out var side, out var outlet);

            if (!TryComp<AppearanceComponent>(uid, out var appearance))
                return;

            // if (filter.InletName == filter.OutletName)
            //     inlet = outlet;

            if (core != null)
                _appearanceSystem.SetData(uid, FilterVisuals.Core, core, appearance);
            if (inlet != null)
                _appearanceSystem.SetData(uid, FilterVisuals.Inlet, inlet, appearance);
            if (outlet != null)
                _appearanceSystem.SetData(uid, FilterVisuals.Outlet, outlet, appearance);
            if (side != null)
                _appearanceSystem.SetData(uid, FilterVisuals.Side, side, appearance);
        }

        private void DoFilterUpdated(EntityUid uid, GasFilterComponent filter, ref AtmosDeviceUpdateEvent args,
            out FilterPortVisualsState? coreVisual,
            out FilterPortVisualsState? inletVisual,
            out FilterPortVisualsState? filterVisual,
            out FilterPortVisualsState? outletVisual)
        {
            coreVisual = null;
            inletVisual = null;
            filterVisual = null;
            outletVisual = null;

            // STARLIGHT - Disable outlet node pressure check for inline filter
            if (!filter.Enabled)
            {
                _ambientSoundSystem.SetAmbience(uid, false);
                return;
            }

            if (!_nodeContainer.TryGetNodes(uid, filter.InletName, filter.FilterName, filter.OutletName,
                    out PipeNode? inletNode, out PipeNode? filterNode, out PipeNode? outletNode))
                return;

            coreVisual = FilterPortVisualsState.Off;
            inletVisual = FilterPortVisualsState.Off;
            filterVisual = FilterPortVisualsState.Off;
            outletVisual = FilterPortVisualsState.Off;

            var inlet = inletNode.Air;
            var totalMoles = inlet.TotalMoles;

            if (totalMoles <= DeltaMolCutoff)
            {
                coreVisual = FilterPortVisualsState.SolidYellow;
                inletVisual = FilterPortVisualsState.SolidYellow;
                _ambientSoundSystem.SetAmbience(uid, false);
                return;
            }

            var filterableMoles = 0f;
            foreach (var gas in filter.FilteredGases)
                filterableMoles += inlet.GetMoles(gas);
            var nonFilterableMoles = Math.Max(0f, totalMoles - filterableMoles);

            // One shared per-tick pull budget, in moles - same derivation as before, just computed
            // once for the whole device instead of once per destination.
            var wantToTransferVolume = filter.TransferRate * _atmosphereSystem.PumpSpeedup() * args.dt;
            var pulledVolume = Math.Min(inlet.Volume, wantToTransferVolume);
            var budgetMoles = inlet.Pressure * pulledVolume / (inlet.Temperature * Atmospherics.R);

            // --- Filter/side pass: gets first claim on the budget ---
            var filterSpaceLeft = MolesSpaceLeft(filterNode);
            var moveToFilter = Math.Clamp(Math.Min(filterableMoles, budgetMoles), 0f, filterSpaceLeft);

            var toFilter = new GasMixture { Temperature = inlet.Temperature };
            if (moveToFilter > 0f)
            {
                var scale = moveToFilter / filterableMoles;
                foreach (var gas in filter.FilteredGases)
                {
                    var amount = inlet.GetMoles(gas) * scale;
                    inlet.AdjustMoles(gas, -amount);
                    toFilter.AdjustMoles(gas, amount);
                }
            }

            var leftoverFilterableMoles = Math.Max(0f, filterableMoles - moveToFilter);

            // --- Outlet pass: whatever's left of the budget after the filter pass ---
            var remainingBudget = Math.Max(0f, budgetMoles - moveToFilter);
            var outletCandidateMoles = nonFilterableMoles + (filter.Passthrough ? leftoverFilterableMoles : 0f);
            var outletSpaceLeft = outletNode == inletNode ? float.PositiveInfinity : MolesSpaceLeft(outletNode);
            var moveToOutlet = Math.Clamp(Math.Min(outletCandidateMoles, remainingBudget), 0f, outletSpaceLeft);

            var toOutlet = new GasMixture { Temperature = inlet.Temperature };
            if (moveToOutlet > 0f)
            {
                var scale = moveToOutlet / outletCandidateMoles;
                foreach (var (gas, moles) in inlet)
                {
                    if (moles <= 0f)
                        continue;
                    if (!filter.Passthrough && filter.FilteredGases.Contains(gas))
                        continue;

                    var amount = moles * scale;
                    inlet.AdjustMoles(gas, -amount);
                    toOutlet.AdjustMoles(gas, amount);
                }
            }

            _atmosphereSystem.Merge(filterNode.Air, toFilter);
            _atmosphereSystem.Merge(outletNode.Air, toOutlet);

            // Each port is Green if its pass actually moved gas, BlinkingRed if it had a non-trivial
            // candidate pool but couldn't move any of it (destination full), else Off (idle).
            var sideFlowing = moveToFilter > DeltaMolCutoff;
            var sideBlocked = !sideFlowing && filterableMoles > DeltaMolCutoff;
            if (sideFlowing)
                filterVisual = FilterPortVisualsState.SolidGreen;
            else if (sideBlocked)
                filterVisual = FilterPortVisualsState.BlinkingRed;

            var outletFlowing = moveToOutlet > DeltaMolCutoff;
            var outletBlocked = !outletFlowing && outletCandidateMoles > DeltaMolCutoff;
            if (outletFlowing)
                outletVisual = FilterPortVisualsState.SolidGreen;
            else if (outletBlocked)
                outletVisual = FilterPortVisualsState.BlinkingRed;

            // Core reflects the device overall, not any single port: green if either path is
            // actually moving gas, red only if something is blocked and nothing else is flowing.
            if (outletFlowing || sideFlowing)
                coreVisual = FilterPortVisualsState.SolidGreen;
            else if (outletBlocked || sideBlocked)
                coreVisual = FilterPortVisualsState.BlinkingRed;

            _ambientSoundSystem.SetAmbience(uid, sideFlowing || outletFlowing);
        }

        /// <summary>
        /// How many more moles a destination node can accept before hitting MaxOutputPressure.
        /// </summary>
        private static float MolesSpaceLeft(PipeNode node)
        {
            var spaceLeft = (Atmospherics.MaxOutputPressure - node.Air.Pressure) * node.Air.Volume /
                            (node.Air.Temperature * Atmospherics.R);
            return Math.Max(0f, spaceLeft);
        }

        private void OnAnchorChanged(EntityUid uid, GasFilterComponent filter, ref AnchorStateChangedEvent args)
        {
            if (!args.Anchored)
            {
                filter.Enabled = false;
                UpdateAppearance(uid, filter);
                _ambientSoundSystem.SetAmbience(uid, false);
                DirtyUI(uid, filter);
            }
        }
        // Starlight End

        private void OnFilterLeaveAtmosphere(EntityUid uid, GasFilterComponent filter, ref AtmosDeviceDisabledEvent args)
        {
            // filter.Enabled = false; // Starlight Edit: Moved to OnAnchorChanged

            UpdateAppearance(uid, filter);
            _ambientSoundSystem.SetAmbience(uid, false);

            DirtyUI(uid, filter);
            _userInterfaceSystem.CloseUi(uid, GasFilterUiKey.Key);
        }

        private void OnFilterActivate(EntityUid uid, GasFilterComponent filter, ActivateInWorldEvent args)
        {
            if (args.Handled || !args.Complex)
                return;

            if (!TryComp(args.User, out ActorComponent? actor))
                return;

            if (Comp<TransformComponent>(uid).Anchored)
            {
                _userInterfaceSystem.OpenUi(uid, GasFilterUiKey.Key, actor.PlayerSession);
                DirtyUI(uid, filter);
            }
            else
            {
                _popupSystem.PopupCursor(Loc.GetString("comp-gas-filter-ui-needs-anchor"), args.User);
            }

            args.Handled = true;
        }

        private void DirtyUI(EntityUid uid, GasFilterComponent? filter)
        {
            if (!Resolve(uid, ref filter))
                return;

            _userInterfaceSystem.SetUiState(uid, GasFilterUiKey.Key,
                new GasFilterBoundUserInterfaceState(MetaData(uid).EntityName, filter.TransferRate, filter.Enabled, filter.FilteredGases));
        }

        private void UpdateAppearance(EntityUid uid, GasFilterComponent? filter = null)
        {
            if (!Resolve(uid, ref filter, false))
                return;

            _appearanceSystem.SetData(uid, FilterVisuals.Enabled, filter.Enabled);
        }

        private void OnToggleStatusMessage(EntityUid uid, GasFilterComponent filter, GasFilterToggleStatusMessage args)
        {
            filter.Enabled = args.Enabled;
            _adminLogger.Add(LogType.AtmosPowerChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} set the power on {ToPrettyString(uid):device} to {args.Enabled}");
            DirtyUI(uid, filter);
            UpdateAppearance(uid, filter);
        }

        private void OnTransferRateChangeMessage(EntityUid uid, GasFilterComponent filter, GasFilterChangeRateMessage args)
        {
            filter.TransferRate = Math.Clamp(args.Rate, 0f, filter.MaxTransferRate);
            _adminLogger.Add(LogType.AtmosVolumeChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} set the transfer rate on {ToPrettyString(uid):device} to {args.Rate}");
            DirtyUI(uid, filter);

        }

        private void OnAddGasMessage(EntityUid uid, GasFilterComponent filter, GasFilterAddGasMessage args)
        {
            if (!Enum.IsDefined(typeof(Gas), args.Gas))
            {
                Log.Warning($"{ToPrettyString(uid)} received GasFilterAddGasMessage with an invalid ID: {args.Gas}");
                return;
            }

            if (!filter.FilteredGases.Add(args.Gas))
                return;

            _adminLogger.Add(LogType.AtmosFilterChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} added {args.Gas} to the filter on {ToPrettyString(uid):device}");
            DirtyUI(uid, filter);
        }

        private void OnRemoveGasMessage(EntityUid uid, GasFilterComponent filter, GasFilterRemoveGasMessage args)
        {
            if (!filter.FilteredGases.Remove(args.Gas))
                return;

            _adminLogger.Add(LogType.AtmosFilterChanged, LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} removed {args.Gas} from the filter on {ToPrettyString(uid):device}");
            DirtyUI(uid, filter);
        }

        /// <summary>
        /// Returns the gas mixture for the gas analyzer
        /// </summary>
        private void OnFilterAnalyzed(EntityUid uid, GasFilterComponent component, GasAnalyzerScanEvent args)
        {
            args.GasMixtures ??= new List<(string, GasMixture?)>();

            // multiply by volume fraction to make sure to send only the gas inside the analyzed pipe element, not the whole pipe system
            if (_nodeContainer.TryGetNode(uid, component.InletName, out PipeNode? inlet) && inlet.Air.Volume != 0f)
            {
                var inletAirLocal = inlet.Air.Clone();
                inletAirLocal.Multiply(inlet.Volume / inlet.Air.Volume);
                inletAirLocal.Volume = inlet.Volume;
                args.GasMixtures.Add((Loc.GetString("gas-analyzer-window-text-inlet"), inletAirLocal));
            }
            if (_nodeContainer.TryGetNode(uid, component.FilterName, out PipeNode? filterNode) && filterNode.Air.Volume != 0f)
            {
                var filterNodeAirLocal = filterNode.Air.Clone();
                filterNodeAirLocal.Multiply(filterNode.Volume / filterNode.Air.Volume);
                filterNodeAirLocal.Volume = filterNode.Volume;
                args.GasMixtures.Add((Loc.GetString("gas-analyzer-window-text-filter"), filterNodeAirLocal));
            }
            if (_nodeContainer.TryGetNode(uid, component.OutletName, out PipeNode? outlet) && outlet.Air.Volume != 0f)
            {
                var outletAirLocal = outlet.Air.Clone();
                outletAirLocal.Multiply(outlet.Volume / outlet.Air.Volume);
                outletAirLocal.Volume = outlet.Volume;
                args.GasMixtures.Add((Loc.GetString("gas-analyzer-window-text-outlet"), outletAirLocal));
            }

            // STARLIGHT START
            // if inlet and outlet are the same you cant get a direction from it
            if (inlet == outlet)
                return;
            // STARLIGHT END

            args.DeviceFlipped = inlet != null && filterNode != null && inlet.CurrentPipeDirection.ToDirection() == filterNode.CurrentPipeDirection.ToDirection().GetClockwise90Degrees();
        }
    }
}
