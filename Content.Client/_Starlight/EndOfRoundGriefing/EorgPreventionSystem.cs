using Content.Shared._Starlight.EndOfRoundGriefing;

namespace Content.Client._Starlight.EndOfRoundGriefing;

/// <inheritdoc/>
public sealed partial class EorgPreventionSystem : SharedEorgPreventionSystem
{
    public override void Initialize()
    {
        base.Initialize();
        RaiseNetworkEvent(new RequestEorgPreventionStateEvent()); // Ask server to send a state.
    }

    [SubscribeNetworkEvent]
    private void OnStateChanged(EorgPreventionStateEvent ev)
    {
        IsEnabled = ev.IsEnabled;
        HasRoundEnded = ev.HasRoundEnded;
        IsPending = ev.IsPending;
    }
}
