using Content.Server.DeviceLinking.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Interaction;
using Content.Shared.Lock;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Gates the quarantine alarm button on Virology access and drives its LED from switch state.
/// </summary>
public sealed partial class VirologyAlarmButtonSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private LockSystem _lock = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VirologyAlarmButtonComponent, MapInitEvent>(OnMapInit);
        // LED is set to the post-toggle state before SignalSwitchSystem applies it.
        SubscribeLocalEvent<VirologyAlarmButtonComponent, ActivateInWorldEvent>(OnActivate,
            before: [typeof(SignalSwitchSystem)]);
    }

    private void OnMapInit(Entity<VirologyAlarmButtonComponent> ent, ref MapInitEvent args)
    {
        UpdateIndicator(ent);
    }

    private void OnActivate(Entity<VirologyAlarmButtonComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        // Let LockSystem process an access-authorized unlock. A later press activates
        // the alarm once the button is unlocked, matching other lockable buttons.
        if (_lock.IsLocked(ent.Owner))
            return;

        // SignalSwitchSystem flips State after this handler; preview that value for the LED.
        var upcomingOn = !(TryComp<SignalSwitchComponent>(ent, out var signal) && signal.State);
        _appearance.SetData(ent, VirologyAlarmButtonVisuals.On, upcomingOn);
    }

    private void UpdateIndicator(EntityUid uid)
    {
        var on = TryComp<SignalSwitchComponent>(uid, out var signal) && signal.State;
        _appearance.SetData(uid, VirologyAlarmButtonVisuals.On, on);
    }
}
