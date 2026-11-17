using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.Utility.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DelayedEventComponent : Component
{
    [DataField]
    public string EventId = string.Empty;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan TriggerTime;
}
