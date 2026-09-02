using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starfall.Traits.Assorted;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
[Access(typeof(MigraineSchedulerSystem))]
public sealed partial class MigraineSchedulerComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public TimeSpan MinTimeBetweenIncidents = TimeSpan.FromMinutes(10);

    [DataField(required: true), AutoNetworkedField]
    public TimeSpan MaxTimeBetweenIncidents = TimeSpan.FromMinutes(30);

    [DataField(required: true), AutoNetworkedField]
    public TimeSpan MinDurationOfIncident = TimeSpan.FromSeconds(8);

    [DataField(required: true), AutoNetworkedField]
    public TimeSpan MaxDurationOfIncident = TimeSpan.FromSeconds(12);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextIncidentTime;
}
