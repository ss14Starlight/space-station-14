using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Changeling;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private RespiratorSystem _respirator = default!;

    [SubscribeLocalEvent]
    private void OnToggleBreathing(EntityUid uid, ChangelingComponent comp, ref ToggleChangelingBreathingEvent args)
    {
        if (args.Handled || !_respirator.SetEnabled(uid, !comp.BreathingEnabled))
            return;

        comp.BreathingEnabled = !comp.BreathingEnabled;
        _actions.SetToggled(args.Action.AsNullable(), !comp.BreathingEnabled);
        Dirty(uid, comp);
        _popup.PopupEntity(Loc.GetString(comp.BreathingEnabled ? "changeling-breathing-enabled" : "changeling-breathing-disabled"), uid, uid);
        args.Handled = true;
    }
}
