using Content.Server._Moffstation.Atmos.Piping.Trinary.Components;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Atmos.Piping.Trinary.Components;
using Content.Shared.Audio;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Moffstation.Atmos.Piping.Trinary.EntitySystems;

/// <summary>
/// This handle the <see cref="GasMixerMolarComponent"/>. The majority of the functionalities
/// are identical to <see cref="GasMixerSystem"/>, the only difference being the behavior of
/// the OnMixerUpdated method.
/// </summary>
public sealed partial class GasMixerMolarSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambientSoundSystem = default!;
    [Dependency] private NodeContainerSystem _nodeContainer = default!;
    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;


    [SubscribeLocalEvent]
    private void OnInit(Entity<GasMixerMolarComponent> ent, ref ComponentInit args)
    {
        UpdateAppearance(ent);
    }

    [SubscribeLocalEvent]
    private void OnMixerUpdated(Entity<GasMixerMolarComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!ent.Comp.Enabled ||
            !_nodeContainer.TryGetNodes(ent.Owner,
                ent.Comp.InletOneName,
                ent.Comp.InletTwoName,
                ent.Comp.OutletName,
                out PipeNode? inletOne,
                out PipeNode? inletTwo,
                out PipeNode? outlet))
            {
                _ambientSoundSystem.SetAmbience(ent, false);
                return;
            }

            if (outlet.Air.Pressure >= ent.Comp.TargetPressure) // no need to mix
                return;

            // step 1 : Compute the maximum number of moles that can be provided by the two input nodes.
            //          These quantities will respect the requested concentrations.
            var maxMolesOne = inletOne.Air.TotalMoles;
            var maxMolesTwo = inletTwo.Air.TotalMoles;

            if (ent.Comp.InletTwoConcentration > 0)
                maxMolesOne = MathF.Min(maxMolesOne, inletTwo.Air.TotalMoles * (ent.Comp.InletOneConcentration / ent.Comp.InletTwoConcentration));

            if (ent.Comp.InletOneConcentration > 0)
                maxMolesTwo = MathF.Min(maxMolesTwo, inletOne.Air.TotalMoles * (ent.Comp.InletTwoConcentration / ent.Comp.InletOneConcentration));

            // step 2 : create a gas mixture from the content of the two inlets.
            //          compute the amount of this mixture to be transferred to the outlet using PV=nRT.
            var transferableInletOne = inletOne.Air.Remove(maxMolesOne);
            var transferableInletTwo = inletTwo.Air.Remove(maxMolesTwo);

            var transferMixture = new GasMixture();

            _atmosphereSystem.Merge(transferMixture, transferableInletOne);
            _atmosphereSystem.Merge(transferMixture, transferableInletTwo);

            var pressureDelta = ent.Comp.TargetPressure - outlet.Air.Pressure;
            var totalTransferredMoles = (pressureDelta * outlet.Air.Volume) / (transferMixture.Temperature * Atmospherics.R);

            // step 3 : transfer gas from inlets using the total transferred mole amount and the requested concentrations.
            _atmosphereSystem.Merge(outlet.Air, transferableInletOne.Remove(totalTransferredMoles * ent.Comp.InletOneConcentration));
            _atmosphereSystem.Merge(outlet.Air, transferableInletTwo.Remove(totalTransferredMoles * ent.Comp.InletTwoConcentration));

            _atmosphereSystem.Merge(inletOne.Air, transferableInletOne);
            _atmosphereSystem.Merge(inletTwo.Air, transferableInletTwo);

            _ambientSoundSystem.SetAmbience(ent.Owner, totalTransferredMoles > 0);
    }

    [SubscribeLocalEvent]
    private void OnMixerLeaveAtmosphere(Entity<GasMixerMolarComponent> ent, ref AtmosDeviceDisabledEvent args)
    {
        ent.Comp.Enabled = false;

        DirtyUi(ent);
        UpdateAppearance(ent);
        _userInterfaceSystem.CloseUi(ent.Owner, GasFilterUiKey.Key);
    }

    [SubscribeLocalEvent]
    private void OnMixerActivate(Entity<GasMixerMolarComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (Transform(ent).Anchored)
        {
            _userInterfaceSystem.OpenUi(ent.Owner, GasMixerUiKey.Key, actor.PlayerSession);
            DirtyUi(ent);
        }
        else
        {
            _popup.PopupCursor(Loc.GetString("comp-gas-mixer-ui-needs-anchor"), args.User);
        }

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnMixerGasAnalyzed(Entity<GasMixerMolarComponent> ent, ref GasAnalyzerScanEvent args)
    {
        args.GasMixtures ??= [];

        // multiply by volume fraction to make sure to send only the gas inside the analyzed pipe element, not the whole pipe system
        if (_nodeContainer.TryGetNode(ent.Owner, ent.Comp.InletOneName, out PipeNode? inletOne) && inletOne.Air.Volume != 0f)
        {
            var inletOneAirLocal = inletOne.Air.Clone();
            inletOneAirLocal.Multiply(inletOne.Volume / inletOne.Air.Volume);
            inletOneAirLocal.Volume = inletOne.Volume;
            args.GasMixtures.Add(($"{inletOne.CurrentPipeDirection} {Loc.GetString("gas-analyzer-window-text-inlet")}", inletOneAirLocal));
        }
        if (_nodeContainer.TryGetNode(ent.Owner, ent.Comp.InletTwoName, out PipeNode? inletTwo) && inletTwo.Air.Volume != 0f)
        {
            var inletTwoAirLocal = inletTwo.Air.Clone();
            inletTwoAirLocal.Multiply(inletTwo.Volume / inletTwo.Air.Volume);
            inletTwoAirLocal.Volume = inletTwo.Volume;
            args.GasMixtures.Add(($"{inletTwo.CurrentPipeDirection} {Loc.GetString("gas-analyzer-window-text-inlet")}", inletTwoAirLocal));
        }
        if (_nodeContainer.TryGetNode(ent.Owner, ent.Comp.OutletName, out PipeNode? outlet) && outlet.Air.Volume != 0f)
        {
            var outletAirLocal = outlet.Air.Clone();
            outletAirLocal.Multiply(outlet.Volume / outlet.Air.Volume);
            outletAirLocal.Volume = outlet.Volume;
            args.GasMixtures.Add((Loc.GetString("gas-analyzer-window-text-outlet"), outletAirLocal));
        }

        args.DeviceFlipped = inletOne != null && inletTwo != null && inletOne.CurrentPipeDirection.ToDirection() == inletTwo.CurrentPipeDirection.ToDirection().GetClockwise90Degrees();
    }

    // Ui messages

    [SubscribeLocalEvent]
    private void OnOutputPressureChange(Entity<GasMixerMolarComponent> ent,
        ref GasMixerChangeOutputPressureMessage args)
    {
        ent.Comp.TargetPressure = Math.Clamp(args.Pressure, 0f, ent.Comp.MaxTargetPressure);
        _adminLogger.Add(
            LogType.AtmosPressureChanged,
            LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the pressure on {ToPrettyString(ent.Owner):device} to {args.Pressure}kPa");
        DirtyUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnNodePercentageChanged(Entity<GasMixerMolarComponent> ent,
        ref GasMixerChangeNodePercentageMessage args)
    {
        var nodeOne = Math.Clamp(args.NodeOne, 0f, 100.0f) / 100.0f;
        ent.Comp.InletOneConcentration = nodeOne;
        ent.Comp.InletTwoConcentration = 1.0f - ent.Comp.InletOneConcentration;
        _adminLogger.Add(
            LogType.AtmosRatioChanged,
            LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the ratio on {ToPrettyString(ent.Owner):device} to {ent.Comp.InletOneConcentration}:{ent.Comp.InletTwoConcentration}");
        DirtyUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnStatusToggled(Entity<GasMixerMolarComponent> ent, ref GasMixerToggleStatusMessage args)
    {
        ent.Comp.Enabled = args.Enabled;
        _adminLogger.Add(
            LogType.AtmosPowerChanged,
            LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set the power on {ToPrettyString(ent.Owner):device} to {args.Enabled}");
        DirtyUi(ent);
        UpdateAppearance(ent);
    }

    // Helpers

    private void UpdateAppearance(Entity<GasMixerMolarComponent> ent)
    {
        if (! TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        _appearance.SetData(ent, FilterVisuals.Enabled, ent.Comp.Enabled, appearance);
    }

    private void DirtyUi(Entity<GasMixerMolarComponent> ent)
    {
        _userInterfaceSystem.SetUiState(
            ent.Owner,
            GasMixerUiKey.Key,
            new GasMixerBoundUserInterfaceState(
                Comp<MetaDataComponent>(ent).EntityName,
                ent.Comp.TargetPressure,
                ent.Comp.Enabled,
                ent.Comp.InletOneConcentration));
    }

}
