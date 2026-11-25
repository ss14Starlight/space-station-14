using Content.Server.Atmos.Components;
using Content.Server._Starlight.Atmos.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

// ReSharper disable CheckNamespace
namespace Content.Server.Atmos.EntitySystems;

public sealed partial class HeatExchangerSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnInlineHeatExchangerUpdate(EntityUid uid, InlineHeatExchangerComponent comp, ref AtmosDeviceUpdateEvent args)
    {
        if (args.Grid is { } grid
            && _transform.TryGetGridTilePosition(uid, out var tile)
            && _atmosphereSystem.IsTileAirBlockedCached(grid, tile))
            return;

        // this section is the main inline-specific bit
        // it gets the gas mixture belonging to the entire connected pipe network instead of splitting it in two
        if (!_nodeContainer.TryGetNode<PipeNode>(uid, comp.NodeName, out var pipe))
            return;

        if (pipe?.NodeGroup is not PipeNet pipeNet)
            return;
        var air = pipeNet.Air;

        // onwards from here, everything is identical to upstream but using "air" instead of "xfer" as there is no transfer in an inline radiator
        var dt = args.dt;
        float CAir = _atmosphereSystem.GetHeatCapacity(air, true);

        if (CAir < Atmospherics.MinimumHeatCapacity)
            return;

        var environment = _atmosphereSystem.GetContainingMixture(uid, true, true);
        bool hasEnv = false;
        float CEnv = 0f;
        var radTemp = Atmospherics.TCMB;

        if (environment != null)
        {
            CEnv = _atmosphereSystem.GetHeatCapacity(environment, true);
            hasEnv = CEnv >= Atmospherics.MinimumHeatCapacity && environment.TotalMoles > 0f;
            if (hasEnv)
                radTemp = environment.Temperature;
        }

        float TdivQ = 1f / CAir;

        if (hasEnv)
            TdivQ += 1f / CEnv;

        // Radiation
        float dTR = air.Temperature - radTemp;
        float dTRA = MathF.Abs(dTR);
        float a0 = tileLoss / MathF.Pow(Atmospherics.T20C, 4);
        float kR = comp.alpha * a0 * TdivQ;
        float dT2R = dTR * MathF.Pow((1f + (3f * kR * dt * dTRA * dTRA * dTRA)), -1f / 3f);
        float dER = (dTR - dT2R) / TdivQ;

        _atmosphereSystem.AddHeat(air, -dER);

        if (hasEnv && environment != null)
        {
            _atmosphereSystem.AddHeat(environment, dER);
            // Convection
            float dT = air.Temperature - environment.Temperature;
            float k = comp.K * TdivQ;
            float dT2 = dT * MathF.Exp(-k * dt);
            float dE = (dT - dT2) / TdivQ;
            _atmosphereSystem.AddHeat(air, -dE);
            _atmosphereSystem.AddHeat(environment, dE);
        }
    }
}
