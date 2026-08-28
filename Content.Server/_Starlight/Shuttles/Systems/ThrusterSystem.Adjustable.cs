using Content.Server._Starlight.Shuttles.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Starlight.Shuttles;
using Robust.Server.GameObjects;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ThrusterSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    private void InitializeAdjustable()
    {
        SubscribeLocalEvent<AdjustableThrusterComponent, MapInitEvent>(OnAdjustableMapInit);
        SubscribeLocalEvent<AdjustableThrusterComponent, BoundUIOpenedEvent>(OnAdjustableUiOpened);
        SubscribeLocalEvent<AdjustableThrusterComponent, AdjustableThrusterSetThrustMessage>(OnAdjustableSetThrust);
    }

    private void OnAdjustableMapInit(Entity<AdjustableThrusterComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.BaseThrust <= 0f && TryComp(ent, out ThrusterComponent? thruster))
            ent.Comp.BaseThrust = thruster.Thrust;
    }

    private void OnAdjustableUiOpened(Entity<AdjustableThrusterComponent> ent, ref BoundUIOpenedEvent args)
        => UpdateAdjustableUi(ent);

    private void OnAdjustableSetThrust(Entity<AdjustableThrusterComponent> ent, ref AdjustableThrusterSetThrustMessage args)
    {
        if (!TryComp(ent, out ThrusterComponent? thruster))
            return;

        var (min, max) = GetThrustBounds(ent.Comp);
        SetThrust((ent.Owner, thruster), Math.Clamp(args.Thrust, min, max));

        UpdateAdjustableUi(ent);
    }

    /// <summary>
    /// Sets a thruster's thrust, keeping its shuttle's cached thrust totals in sync.
    /// </summary>
    public void SetThrust(Entity<ThrusterComponent> ent, float thrust)
    {
        if (MathHelper.CloseTo(ent.Comp.Thrust, thrust))
            return;

        var wasOn = ent.Comp.IsOn;

        if (wasOn)
            DisableThruster(ent, ent.Comp);

        ent.Comp.Thrust = thrust;

        if (wasOn)
            EnableThruster(ent, ent.Comp);
    }

    private void UpdateAdjustableUi(Entity<AdjustableThrusterComponent> ent)
    {
        if (!TryComp(ent, out ThrusterComponent? thruster))
            return;

        var (min, max) = GetThrustBounds(ent.Comp);
        _ui.SetUiState(ent.Owner, AdjustableThrusterUiKey.Key, new AdjustableThrusterBuiState(thruster.Thrust, min, max));
    }

    private static (float Min, float Max) GetThrustBounds(AdjustableThrusterComponent component)
        => (component.MinThrust, MathF.Max(component.MinThrust, component.BaseThrust * component.MaxMultiplier));
}
