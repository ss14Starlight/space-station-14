using Content.Shared._Starlight.Weapons.Cover.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Weapons.Cover.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedProjectileCoverSystem))]
public sealed partial class ProjectileCoverComponent : Component
{
    /// <summary>
    /// How much chance do we have to block bullet
    /// </summary>

    [DataField, AutoNetworkedField]
    public float BlockChance = 0.4f;

    /// <summary>
    /// Low cover (small crates, counters) that only protects someone lying down right behind it.
    /// Shots at a standing target always pass over it, whatever <see cref="BlockChance"/> says.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ProneOnly;

    /// <summary>
    /// If true, cover will block all shots at prone targets, regardless of <see cref="BlockChance"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ProneAlwaysBlock = true;

    /// <summary>
    /// Determines range on which we will ignore players, so player near cover can shoot through it.
    /// </summary>

    [DataField, AutoNetworkedField]
    public float PointBlankRange = 1.5f;

    /// <summary>
    /// Determines range on which layed down player will have 100% block chance.
    /// </summary>

    [DataField, AutoNetworkedField]
    public float ShelterRange = 1.5f;

    /// <summary>
    /// Determines what bullets will be blocked(if not null)
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Determines what bullets won't be blocked(if not null)
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;
}
