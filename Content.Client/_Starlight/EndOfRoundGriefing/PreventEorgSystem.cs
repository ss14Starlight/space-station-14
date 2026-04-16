using Content.Shared._Starlight.EndOfRoundGriefing;

namespace Content.Client._Starlight.EndOfRoundGriefing;

/// <summary>
/// <inheritdoc/>
/// </summary>
public sealed class EorgPreventionSystem : SharedEorgPreventionSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PreventEorgStateEvent>(OnStateChanged); // Handle server state changes.
        RaiseNetworkEvent(new RequestPreventEorgStateEvent()); // Ask server to send a state.
    }

    private void OnStateChanged(PreventEorgStateEvent ev)
    {
        IsEnabled = ev.IsEnabled;
        HasRoundEnded = ev.IsRoundEnded;
    }
}
