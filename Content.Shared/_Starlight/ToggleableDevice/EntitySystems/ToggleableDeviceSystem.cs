using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using ToggleableDeviceComponent = Content.Shared._Starlight.ToggleableDevice.Components.ToggleableDeviceComponent;

namespace Content.Shared._Starlight.ToggleableDevice.EntitySystems;

/// <summary>
/// Controls the on/off state for a device.
/// </summary>
public sealed partial class ToggleableDeviceSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

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

        ent.Comp.Enabled = state;
        RaiseLocalEvent(ent, new ToggleableStateEvent(state));
        Dirty(ent, ent.Comp);

        _adminLogger.Add(ent.Comp.LogType,
            LogImpact.Medium,
            $"{ToPrettyString(actor):player} set the power on {ToPrettyString(ent):device} to {state}");
    }

    public void ForceState(Entity<ToggleableDeviceComponent?> ent, bool state)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.Enabled = state;
        Dirty(ent, ent.Comp);
    }

    #endregion
    #region Logging

    public void SetLogType(Entity<ToggleableDeviceComponent?> ent, LogType logType)
    {
        if (Resolve(ent, ref ent.Comp, false))
            ent.Comp.LogType = logType;
    }

    #endregion

}

public record struct ToggleableStateEvent(bool State);
