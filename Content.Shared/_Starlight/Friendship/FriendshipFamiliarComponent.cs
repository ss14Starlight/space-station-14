using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Friendship;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FriendshipFamiliarComponent : Component
{
    [DataField]
    public EntProtoId MobToSpawn = "SpawnPointGhostFamiliarPlayerSpecies";

    [ViewVariables, AutoNetworkedField]
    public EntityUid? SpawnedMob;
}