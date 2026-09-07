using Content.Shared.Actions;

namespace Content.Shared._Starlight.Silicons.Borgs;

public sealed partial class StationAIShuntSystem
{
    private void InitializeReconnect()
    {
        SubscribeLocalEvent<StationAIShuntableComponent, AIReconnectShuntActionEvent>(OnAttemptReconnect);
    }

    private void OnAttemptReconnect(EntityUid uid, StationAIShuntableComponent shuntable, AIReconnectShuntActionEvent ev)
    {
        if (ev.Handled || shuntable.Inhabited.HasValue || shuntable.LastShunt is not { } lastShunt || Deleted(lastShunt))
            return;

        var shuntEv = new AIShuntActionEvent
        {
            Target = lastShunt,
            Performer = uid,
            IgnoreCameraView = true
        };
        RaiseLocalEvent(uid, shuntEv);
        ev.Handled = shuntEv.Handled;
    }
}

public sealed partial class AIReconnectShuntActionEvent : Content.Shared.Actions.InstantActionEvent
{
}
