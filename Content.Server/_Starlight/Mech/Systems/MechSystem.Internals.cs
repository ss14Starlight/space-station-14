using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Mech.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Vehicle.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Mech.Systems;

public sealed partial class MechSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private GasTankSystem _mechGasTank = default!;

    [SubscribeLocalEvent]
    private void OnInhale(Entity<VehicleOperatorComponent> ent, ref InhaleLocationEvent args)
    {
        if (!TryGetPilotMech(ent, out var mech))
            return;
        if (mech.Comp.Internals && mech.Comp.GasTankSlot.ContainedEntity is { } tank &&
            TryComp<GasTankComponent>(tank, out var gasTank) && gasTank.Air.Volume > 0)
        {
            args.Gas = _mechGasTank.RemoveAirVolume((tank, gasTank), args.Respirator.BreathVolume);
            return;
        }
        args.Gas = _atmosphere.GetContainingMixture(mech.Owner);
    }

    [SubscribeLocalEvent]
    private void OnExhale(Entity<VehicleOperatorComponent> ent, ref ExhaleLocationEvent args)
    {
        if (TryGetPilotMech(ent, out var mech))
            args.Gas = _atmosphere.GetContainingMixture(mech.Owner);
    }

    [SubscribeLocalEvent]
    private void OnAtmosExposure(Entity<VehicleOperatorComponent> ent, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled || !TryGetPilotMech(ent, out var mech))
            return;
        args.Gas = mech.Comp.Airtight && TryComp<MechAirComponent>(mech, out var air)
            ? air.Air
            : _atmosphere.GetContainingMixture(mech.Owner, excite: args.Excite);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnMechAir(Entity<MechAirComponent> ent, ref MapInitEvent args)
    {
        var moles = Atmospherics.OneAtmosphere * ent.Comp.Air.Volume / (Atmospherics.R * Atmospherics.T20C);
        ent.Comp.Air.SetMoles(Gas.Frezon, moles);
        ent.Comp.Air.Temperature = Atmospherics.T20C;
        ent.Comp.Air.MarkImmutable();
    }

    [SubscribeLocalEvent]
    private void OnRemoveGasTank(Entity<MechComponent> ent, ref RemoveGasTankEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        _container.EmptyContainer(ent.Comp.GasTankSlot);
        DisableInternals(ent, ent.Comp);
        args.Handled = true;
    }

    private void DisableInternals(EntityUid uid, MechComponent comp)
    {
        comp.Internals = false;
        SetInternalsAction(comp);
        Dirty(uid, comp);
    }

    public void InsertGasTank(EntityUid uid, EntityUid toInsert, MechComponent? component = null, GasTankComponent? gasTank = null)
    {
        if (!Resolve(uid, ref component, false) || !Resolve(toInsert, ref gasTank, false))
            return;
        _container.Insert(toInsert, component.GasTankSlot);
        Dirty(uid, component);
    }
}
