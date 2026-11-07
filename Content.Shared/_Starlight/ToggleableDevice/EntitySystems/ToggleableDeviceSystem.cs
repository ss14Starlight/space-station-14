using Content.Shared._Starlight.ToggleableDevice.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Shared._Starlight.ToggleableDevice.EntitySystems;

/// <summary>
/// Controls the on/off state for a device.
/// </summary>
public sealed partial class ToggleableDeviceSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    /// <summary>
    /// Raise an init state event. Can be used by systems that rely on us to initialize their own enabled/disabled
    /// state properly.
    /// </summary>
    [SubscribeLocalEvent]
    public void OnMapInit(Entity<ToggleableDeviceComponent> ent, ref MapInitEvent ev)
    {
        var preinit = new ToggleableDevicePreInitEvent(ent.Comp.Enabled);
        RaiseLocalEvent(ent, ref preinit);
        ent.Comp.Enabled = preinit.State;

        RaiseLocalEvent(ent, new ToggleableDeviceInitEvent(ent.Comp.Enabled));
    }

    #region State

    public void TurnOn(Entity<ToggleableDeviceComponent?> ent, EntityUid? actor = null)
    {
        if (Resolve(ent, ref ent.Comp, false) && !ent.Comp.Enabled)
            SetState(ent, true, actor);
    }

    public void TurnOff(Entity<ToggleableDeviceComponent?> ent, EntityUid? actor = null)
    {
        if (Resolve(ent, ref ent.Comp, false) && ent.Comp.Enabled)
            SetState(ent, false, actor);
    }

    public void Toggle(Entity<ToggleableDeviceComponent?> ent, EntityUid? actor = null)
    {
        if (Resolve(ent, ref ent.Comp, false))
            SetState(ent, !ent.Comp.Enabled, actor);
    }

    public void SetState(Entity<ToggleableDeviceComponent?> ent, bool state, EntityUid? actor = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;
        if (ent.Comp.Enabled == state)
            return;

        ent.Comp.Enabled = state;
        Dirty(ent, ent.Comp);
        RaiseLocalEvent(ent, new ToggleableDeviceStateChangedEvent(state));

        _adminLogger.Add(ent.Comp.LogType,
            LogImpact.Medium,
            $"{ToPrettyString(actor):player} set the power on {ToPrettyString(ent):device} to {state}");
    }

    /// <summary>
    /// Set the state without firing an event.
    /// </summary>
    public void ForceState(Entity<ToggleableDeviceComponent?> ent, bool state)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.Enabled = state;
        Dirty(ent, ent.Comp);
    }

    #endregion
    #region Logging

    /// <summary>
    /// Changes the log type of state changes. Not persisted.
    /// </summary>
    public void SetLogType(Entity<ToggleableDeviceComponent?> ent, LogType logType)
    {
        if (Resolve(ent, ref ent.Comp, false))
            ent.Comp.LogType = logType;
    }

    #endregion

}

/// <summary>
/// Pre-init event used to determine the device state on init.
/// </summary>
[ByRefEvent]
public record struct ToggleableDevicePreInitEvent(bool State);

/// <summary>
/// Used to inform systems of the initial state of a ToggleableDevice upon load/init.
/// </summary>
public record struct ToggleableDeviceInitEvent(bool State);

/// <summary>
/// Raised when a ToggleableDevice state changes.
/// </summary>
public record struct ToggleableDeviceStateChangedEvent(bool State);
