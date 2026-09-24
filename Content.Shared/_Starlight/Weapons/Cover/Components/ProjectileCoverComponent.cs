using Content.Shared._Starlight.Weapons.Cover.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Weapons.Cover.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedProjectileCoverSystem))]
public sealed partial class ProjectileCoverComponent : Component
{

    [DataField, AutoNetworkedField]
    public float BlockChance = 0.4f;

    [DataField, AutoNetworkedField]
    public float PointBlankRange = 1.5f;

    [DataField, AutoNetworkedField]
    public float ShelterRange = 1.5f;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}
