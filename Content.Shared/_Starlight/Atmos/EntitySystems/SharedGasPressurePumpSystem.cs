using Content.Shared._Starlight.DeviceLinking.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping;
using Robust.Shared.Timing;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedGasPressurePumpSystem
{
    [Dependency] private SharedToggleableDeviceSystem _toggleable = null!;
    [Dependency] private IGameTiming _timing = null!;

    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<GasPressurePumpComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
        {
            _toggleable.TurnOff(ent);
            ent.Comp.Enabled = false;
            Dirty(ent);
            UpdateAppearance(ent);
        }
    }

    [SubscribeLocalEvent]
    private void OnToggleableStateChanged(Entity<GasPressurePumpComponent> ent, ref ToggleableStateEvent ev)
    {
        ent.Comp.Enabled = ev.State;
        Dirty(ent);
        UpdateUi(ent);

        if (_timing.IsFirstTimePredicted)
            UpdateAppearance(ent);
    }

    private void ToggleInternal(Entity<GasPressurePumpComponent> ent)
    {
        _toggleable.Toggle(ent.Owner);
    }

    protected void UpdateAppearance(EntityUid uid, GasPressurePumpComponent? pump = null, AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref pump, ref appearance, false))
            return;

        var pumpOn = pump.Enabled && _receiver.IsPowered(uid);
        _appearance.SetData(uid, PumpVisuals.Enabled, pumpOn, appearance);
    }

    public void Set(EntityUid uid, GasPressurePumpComponent component, bool value)
    {
        if (component.Enabled == value) return;
        component.Enabled = value;
        Dirty(uid, component);
        UpdateAppearance(uid, component);
    }

}
