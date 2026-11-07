using Content.Shared._Starlight.ToggleableDevice.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping;
using Content.Shared.Database;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedGasPressurePumpSystem
{
    [Dependency] private ToggleableDeviceSystem _toggleableDevice = null!;

    [SubscribeLocalEvent]
    private void OnToggleableStateInit(Entity<GasPressurePumpComponent> ent, ref ToggleableStateInitEvent ev)
        => _toggleableDevice.SetLogType(ent.Owner, LogType.AtmosPowerChanged);

    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<GasPressurePumpComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
        {
            _toggleableDevice.TurnOff(ent.Owner);
            ent.Comp.Enabled = false;
            Dirty(ent);
            UpdateAppearance(ent);
        }
    }

    [SubscribeLocalEvent]
    private void OnToggleableStateChanged(Entity<GasPressurePumpComponent> ent, ref ToggleableStateChangedEvent ev)
    {
        ent.Comp.Enabled = ev.State;
        Dirty(ent);
        UpdateUi(ent);
        UpdateAppearance(ent);
    }

    private void ToggleInternal(Entity<GasPressurePumpComponent> ent)
        => _toggleableDevice.Toggle(ent.Owner);

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
