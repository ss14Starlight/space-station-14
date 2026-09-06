using System.Linq;
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
            SubscribeLocalEvent<GasFilterComponent, GasFilterSelectGasMessage>(OnSelectGasMessage);
            SubscribeLocalEvent<GasFilterComponent, GasFilterToggleStatusMessage>(OnToggleStatusMessage);

        }

        private void OnInit(EntityUid uid, GasFilterComponent filter, ComponentInit args)
        {
            UpdateAppearance(uid, filter);
        }

        private void OnFilterUpdated(EntityUid uid, GasFilterComponent filter, ref AtmosDeviceUpdateEvent args)
        {
            // STARLIGHT - Disable outlet node pressure check for inline filter
            if (!filter.Enabled
                || !_nodeContainer.TryGetNodes(uid, filter.InletName, filter.FilterName, filter.OutletName, out PipeNode? inletNode, out PipeNode? filterNode, out PipeNode? outletNode)
                || (outletNode.Air.Pressure >= Atmospherics.MaxOutputPressure && filterNode.Air.Pressure >= Atmospherics.MaxOutputPressure)) // No need to transfer if targets are full.
            {
                _ambientSoundSystem.SetAmbience(uid, false);
                return;
            }

            // We multiply the transfer rate in L/s by the seconds passed since the last process to get the liters.
            var transferVol = filter.TransferRate * _atmosphereSystem.PumpSpeedup() * args.dt;

            if (transferVol <= 0)
            {
                _ambientSoundSystem.SetAmbience(uid, false);
                return;
            }

            var removed = inletNode.Air.RemoveVolume(transferVol);
            var transferredMoles = 0f; // Starlight - track total gas moved across both filter outputs

            if (filter.FilteredGases.Count > 0) // Starlight
            {
                var wantsToFilter = new GasMixture(removed.Volume) { Temperature = removed.Temperature };
                SetMixture(filter, removed, wantsToFilter); // Starlight, split all selected gases from passthrough.

                #region Starlight
                // Wizden only handles one selected gas. We need to apply the cap proportionally
                // across all selected gases so multi-gas filters preserve their composition.
                var availableMoles = wantsToFilter.TotalMoles;
                var limitMolesFilter =
                    AtmosphereSystem.MolesToMaxPressure(wantsToFilter, filterNode.Air, Atmospherics.MaxOutputPressure);

                var filteredMoles = Math.Clamp(limitMolesFilter, 0f, availableMoles); // clamp against all selected gases
                var filterRatio = availableMoles > 0f ? filteredMoles / availableMoles : 0f;
                var actuallyFiltered = wantsToFilter.RemoveRatio(filterRatio); // preserve selected-gas ratios
                #endregion

                _atmosphereSystem.Merge(filterNode.Air, actuallyFiltered);
                _atmosphereSystem.Merge(inletNode.Air, wantsToFilter); // Starlight, return selected gas that did not fit.
                transferredMoles += actuallyFiltered.TotalMoles; // Starlight
            }

            if (removed.TotalMoles > 0f) // Starlight
            {
                // Fraction of `removed` that can be sent to outlet without exceeding max pressure.
                var limitRatioOutlet =
                    AtmosphereSystem.FractionToMaxPressure(removed, outletNode.Air, Atmospherics.MaxOutputPressure);

                // This might end up negative, but such cases are handled correctly by the `RemoveRatio` method.
                var passthrough = removed.RemoveRatio(limitRatioOutlet);

                _atmosphereSystem.Merge(outletNode.Air, passthrough);
                transferredMoles += passthrough.TotalMoles; // Starlight
            }

            _atmosphereSystem.Merge(inletNode.Air, removed);
            _ambientSoundSystem.SetAmbience(uid, transferredMoles > 0f); // Starlight
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
                new GasFilterBoundUserInterfaceState(MetaData(uid).EntityName, filter.TransferRate, filter.Enabled, filter.FilteredGases)); // Starlight
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

        private void OnSelectGasMessage(EntityUid uid, GasFilterComponent filter, GasFilterSelectGasMessage args)
        {
            if (args.Gases.Count > 0) // Starlight
            {
                if (args.Gases.All(gas => Enum.IsDefined(gas))) // Starlight
                {
                    filter.FilteredGases = args.Gases; // Starlight: multiple gases
                    _adminLogger.Add(LogType.AtmosFilterChanged, LogImpact.Medium,
                        $"{ToPrettyString(args.Actor):player} set the filter on {ToPrettyString(uid):device} to {ListGases(args)}"); // Starlight: Updated logging
                    DirtyUI(uid, filter);
                }
                else
                {
                    Log.Warning($"{ToPrettyString(uid)} received GasFilterSelectGasMessage with (an) invalid ID(s): {ListGases(args)}"); // Starlight: Updated logging
                }
            }
            else
            {
                filter.FilteredGases.Clear(); // Starlight
                _adminLogger.Add(LogType.AtmosFilterChanged, LogImpact.Medium,
                    $"{ToPrettyString(args.Actor):player} set the filter on {ToPrettyString(uid):device} to none");
                DirtyUI(uid, filter);
            }
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

        #region Starlight

        private void SetMixture(GasFilterComponent component, GasMixture removed, GasMixture wantsToFilter)
        {
            foreach (Gas gas in component.FilteredGases)
            {
                var moles = removed.GetMoles(gas);

                wantsToFilter.SetMoles(gas, moles);
                removed.SetMoles(gas, 0f);
            }
        }

        private static string ListGases(GasFilterSelectGasMessage args) => string.Join(", ", args.Gases);

        public void Set(EntityUid uid, GasFilterComponent component, bool value)
        {
            if (component.Enabled == value) return;
            component.Enabled = value;
            UpdateAppearance(uid, component);
            DirtyUI(uid, component);
        }

        #endregion
    }
}
