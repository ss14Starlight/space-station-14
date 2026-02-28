using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.EntityBeacon.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class EntityBeaconComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public int Range = 0;

    [DataField]
    public int RangeLimit = 40;

    [DataField]
    public List<EntProtoId> EntitiesToSpawn = new();

    public HashSet<EntityCoordinates> CoordinatesToSpawn = new();

    [DataField("nextUpdate", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdateTime;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(10);
}