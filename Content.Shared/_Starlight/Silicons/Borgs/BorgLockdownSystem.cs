using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// Handles borg lock down, which shuts a borg down the same way running out of power does.
/// </summary>
public sealed partial class BorgLockdownSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedBorgSystem _borg = default!;
    [Dependency] private SharedHandheldLightSystem _handheldLight = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgChassisComponent, BorgToggleLockdownBuiMessage>(OnToggleLockdown);
    }

    public bool IsLockedDown(EntityUid borg) => HasComp<BorgLockdownComponent>(borg);

    /// <summary>
    /// Locks down or releases a borg.
    /// </summary>
    public void SetLockedDown(Entity<BorgChassisComponent?> borg, bool lockedDown)
    {
        if (!Resolve(borg, ref borg.Comp) || IsLockedDown(borg) == lockedDown)
            return;

        if (lockedDown)
        {
            EnsureComp<BorgLockdownComponent>(borg);
            _borg.SetActive((borg.Owner, borg.Comp), false);

            if (TryComp<HandheldLightComponent>(borg, out var light))
                _handheldLight.TurnOff((borg.Owner, light), makeNoise: false);
        }
        else
        {
            RemComp<BorgLockdownComponent>(borg);
            _borg.TryActivate((borg.Owner, borg.Comp));
        }

        var popup = lockedDown ? "borg-lockdown-engaged-popup" : "borg-lockdown-released-popup";
        _popup.PopupEntity(Loc.GetString(popup, ("name", Name(borg))), borg);
    }

    private void OnToggleLockdown(Entity<BorgChassisComponent> borg, ref BorgToggleLockdownBuiMessage args)
    {
        var lockedDown = !IsLockedDown(borg);
        SetLockedDown(borg.AsNullable(), lockedDown);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.Actor):player} {(lockedDown ? "locked down" : "released")} borg {ToPrettyString(borg.Owner)}");
    }
}
