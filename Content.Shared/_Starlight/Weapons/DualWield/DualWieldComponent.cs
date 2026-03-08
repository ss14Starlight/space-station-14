using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Weapons.DualWield;

/// <summary>
/// Added to a mob that has activated dual-wield mode with two pistols.
/// Tracks which gun fires next (left/right alternating).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DualWieldComponent : Component
{
    /// <summary>
    /// Whether dual-wield is currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// The gun in the left hand.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid LeftGun;

    /// <summary>
    /// The gun in the right hand.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid RightGun;

    /// <summary>
    /// If true, the next shot fires from the left gun; otherwise the right gun.
    /// Alternates after every shot request.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool NextIsLeft;
}
