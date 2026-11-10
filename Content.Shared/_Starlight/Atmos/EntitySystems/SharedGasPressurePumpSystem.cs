using Content.Shared._Starlight.ToggleableDevice.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping;
using Content.Shared.Audio;
using Content.Shared.Database;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedGasPressurePumpSystem
{
    [Dependency] private ToggleableDeviceSystem _toggleableDevice = null!;
    [Dependency] private SharedAmbientSoundSystem _ambientSound = null!;

    /// <summary>
    /// Use our existing Enabled state for the ToggleableDevice. Required for backwards compatability.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnToggleableDevicePreInit(Entity<GasPressurePumpComponent> ent, ref ToggleableDevicePreInitEvent args)
        => args.State = ent.Comp.Enabled;

    [SubscribeLocalEvent]
    private void OnToggleableDeviceInit(Entity<GasPressurePumpComponent> ent, ref ToggleableDeviceInitEvent ev)
        => _toggleableDevice.SetLogType(ent.Owner, LogType.AtmosPowerChanged);

    [SubscribeLocalEvent]
    private void OnToggleableStateChanged(Entity<GasPressurePumpComponent> ent, ref ToggleableDeviceStateChangedEvent ev)
    {
        ent.Comp.Enabled = ev.State;
        Dirty(ent);
        UpdateUi(ent);
        UpdateAppearance(ent);

        // This is shared code, but the client should never turn *on* ambience based on this.
        // It's only turned on server-side by the code that determines actual gas is moving.
        if (!ev.State)
            _ambientSound.SetAmbience(ent.Owner, ev.State);
    }

    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<GasPressurePumpComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
        {
            _toggleableDevice.TurnOff(ent.Owner);
            ent.Comp.Enabled = false;
            Dirty(ent);
            UpdateAppearance(ent);
            _ambientSound.SetAmbience(ent.Owner, false);
        }
    }

    private void SetStateInternal(Entity<GasPressurePumpComponent> ent, bool state, EntityUid? actor = null)
        => _toggleableDevice.SetState(ent.Owner, state, actor);

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
