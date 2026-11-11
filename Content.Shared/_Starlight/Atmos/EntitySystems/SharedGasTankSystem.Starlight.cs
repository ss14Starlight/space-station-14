using Content.Shared.Atmos.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedGasTankSystem
{
    [Dependency] private INetManager _net = default!;

    [SubscribeLocalEvent]
    private void OnStartup(Entity<GasTankComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient && !IsClientSide(ent.Owner))
            return;

        ent.Comp.TotalMoles = ent.Comp.Air?.TotalMoles ?? 0;
        Dirty(ent);
    }

    public static float GetPredictedPressure(GasTankComponent component)
    {
        var air = component.Air;
        if (air == null || air.Volume <= 0)
            return 0f;
        return component.TotalMoles * Atmospherics.R * air.Temperature / air.Volume;
    }
}
