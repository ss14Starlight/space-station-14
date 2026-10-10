using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Zones;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedZoneTrackerSystem), Other = AccessPermissions.Read)]
public sealed partial class ZoneTrackerComponent : Component
{
    /// <summary>
    /// Main zone of the current room, the highest priority one of <see cref="Zones"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public ProtoId<ZonePrototype>? Zone;

    /// <summary>
    /// Every zone the current room belongs to, e.g. Command and Engineering in the CE office.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<ProtoId<ZonePrototype>> Zones = [];

    /// <summary>
    /// Tiles in the current room, 0 when not in a room (space, off grid). Used for room acoustics.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int RoomSize;

    [ViewVariables]
    public ProtoId<ZonePrototype>? Raised;

    [ViewVariables]
    public (EntityUid Grid, Vector2i Tile) LastPosition;

    [ViewVariables]
    public int LastRevision;
}
