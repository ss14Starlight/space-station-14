using Content.Shared._Starlight.EndOfRoundGriefing;

namespace Content.Client._Starlight.EndOfRoundGriefing;

/// <inheritdoc/>
public sealed class EorgPreventionSystem : SharedEorgPreventionSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<EorgPreventionStateEvent>(OnStateChanged); // Handle server state changes.
        RaiseNetworkEvent(new RequestEorgPreventionStateEvent()); // Ask server to send a state.
    }

    private void OnStateChanged(EorgPreventionStateEvent ev)
    {
        IsEnabled = ev.IsEnabled;
        HasRoundEnded = ev.HasRoundEnded;
    }
}
