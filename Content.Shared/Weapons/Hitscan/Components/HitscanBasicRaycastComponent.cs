using Content.Shared.Physics;
using Robust.Shared.GameStates;
using Content.Shared.Tag; // Starlight
using Robust.Shared.Prototypes; // Starlight

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// A basic raycast system that will shoot in a straight line when triggered.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanBasicRaycastComponent : Component
{
    /// <summary>
    /// Maximum distance the raycast will travel before giving up. Reflections will reset the distance traveled
    /// </summary>
    [DataField]
    public float MaxDistance = 20.0f;

    /// <summary>
    /// The collision mask the hitscan ray uses to collide with other objects. See the enum for more information
    /// </summary>
    [DataField]
    public CollisionGroup CollisionMask = CollisionGroup.Opaque;

    #region Starlight
    /// <summary>
    /// Maximum distance the raycast will travel before giving up. Reflections will reset the distance traveled
    /// </summary>
    [DataField]
    public float MinDistance = 0.0f;

    /// <summary>
    /// What tags entities need to have for the raycast to collide with them prior to it's minimum distance.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>[] NotArmedCollideWith = ["Wall", "Window", "Airlock", "BulletUnarmedCollide"];
    
    /// <summary>
    /// How much attempts we will make for reflect.
    /// </summary>
    [DataField]
    public int Steps = 3;
    #endregion
}
