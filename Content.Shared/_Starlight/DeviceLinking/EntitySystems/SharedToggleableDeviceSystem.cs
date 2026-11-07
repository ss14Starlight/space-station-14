using Content.Shared._Starlight.DeviceLinking.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Shared._Starlight.DeviceLinking.EntitySystems;

public sealed partial class SharedToggleableDeviceSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public void TurnOn(Entity<ToggleableDeviceComponent> ent)
    {
        if (!ent.Comp.Enabled)
            SetState(ent, true);
    }

    public void TurnOff(EntityUid ent)
    {
        if (TryComp<ToggleableDeviceComponent>(ent, out var toggleableDevice))
            TurnOff((ent, toggleableDevice));
    }

    public void TurnOff(Entity<ToggleableDeviceComponent> ent)
    {
        if (ent.Comp.Enabled)
            SetState(ent, false);
    }

    public void Toggle(EntityUid ent)
    {
        if (TryComp<ToggleableDeviceComponent>(ent, out var toggleableDevice))
            Toggle((ent, toggleableDevice));
    }

    public void Toggle(Entity<ToggleableDeviceComponent> ent)
        => SetState(ent, !ent.Comp.Enabled);

    public void SetState(EntityUid uid, bool state)
    {
        if (TryComp<ToggleableDeviceComponent>(uid, out var toggleableDevice))
            SetState((uid, toggleableDevice), state);
    }

    public void SetState(Entity<ToggleableDeviceComponent> ent, bool state, EntityUid? actor = null)
    {
        ent.Comp.Enabled = state;
        RaiseLocalEvent(ent, new ToggleableStateEvent(state));
        Dirty(ent);

        _adminLogger.Add(LogType.AtmosPowerChanged,
            LogImpact.Medium,
            $"{ToPrettyString(actor):player} set the power on {ToPrettyString(ent):device} to {state}");
    }

    public void ForceState(EntityUid uid, bool state)
    {
        if (TryComp<ToggleableDeviceComponent>(uid, out var toggleableDevice))
            ForceState((uid, toggleableDevice), state);
    }

    public void ForceState(Entity<ToggleableDeviceComponent> ent, bool state)
    {
        ent.Comp.Enabled = state;
        Dirty(ent);
    }

    public bool GetState(Entity<ToggleableDeviceComponent> ent)
        => ent.Comp.Enabled;
}

public record struct ToggleableStateEvent(bool State);
