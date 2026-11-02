namespace Content.Shared._Starlight.StationRadio.Components;

/// <summary>
/// Associates a broadcast recorder channel with its recorder.
/// </summary>
[RegisterComponent]
public sealed partial class BroadcastRecorderChannelComponent : Component
{
    [ViewVariables]
    public EntityUid Recorder;
}
