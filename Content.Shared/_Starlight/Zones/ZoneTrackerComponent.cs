using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Zones;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedZoneTrackerSystem), Other = AccessPermissions.Read)]
public sealed partial class ZoneTrackerComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public ProtoId<ZonePrototype>? Zone;

    [ViewVariables]
    public ProtoId<ZonePrototype>? Raised;

    [ViewVariables]
    public (EntityUid Grid, Vector2i Tile) LastPosition;

    [ViewVariables]
    public int LastRevision;
}
