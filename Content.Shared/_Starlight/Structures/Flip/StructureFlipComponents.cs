using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Structures.Flip;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlippableStructureComponent : Component
{
    /// <summary>
    /// Target prototype which will be used to convert entity into
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? FlippedPrototype;

    /// <summary>
    /// How much time it takes to flip entity
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// How much time it takes to unflip entity
    /// </summary>
    [DataField]
    public TimeSpan UnflipDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Chance to cover player from bullets
    /// </summary>
    [DataField]
    public float CoverChance = 0.45f;

    /// <summary>
    /// Determines should we disable power for like vending machines in flipped state
    /// </summary>
    [DataField]
    public bool DisablePower = true;

    /// <summary>
    /// Collision layer which will be used when entity flipped if there's no flipped proto
    /// </summary>

    [DataField(customTypeSerializer: typeof(FlagSerializer<CollisionLayer>))]
    public int FlippedLayer = (int)(CollisionGroup.TableLayer | CollisionGroup.BulletImpassable);

    /// <summary>
    /// Collision mask which will be used when entity flipped if there's no flipped proto
    /// </summary>

    [DataField(customTypeSerializer: typeof(FlagSerializer<CollisionMask>))]
    public int FlippedMask = (int)CollisionGroup.TableMask;

    /// <summary>
    /// Hitbox of the structure lying on its side if there's no flipped proto.
    /// When null, the upright hitbox is turned 90 degrees to follow the sprite.
    /// </summary>
    [DataField]
    public Box2? FlippedBounds;

    /// <summary>
    /// If structure toppled onto another entity, how much damage it will deal to entity.
    /// </summary>
    [DataField]
    public DamageSpecifier? CrushDamage;

    /// <summary>
    /// Determines time on how much entity will be knockeddown after toppling on them
    /// </summary>
    [DataField]
    public TimeSpan CrushKnockdown = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Sound which will be played on toppling
    /// </summary>
    [DataField]
    public SoundSpecifier CrushSound = new SoundPathSpecifier("/Audio/Items/Toys/card_tube_bonk.ogg");

    /// <summary>
    /// Chance of toppling when slipped entity bumps this structure
    /// </summary>
    [DataField]
    public float SlipTopplingChance = 0.5f;

    /// <summary>
    /// Minimal speed of slipped entity required for toppling
    /// </summary>
    [DataField]
    public float SlipMinSpeed = 1.5f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlippedStructureComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? UprightPrototype;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, (int Layer, int Mask)> SavedFixtures = [];

    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, Vector2[]> SavedShapes = [];

    [ViewVariables, AutoNetworkedField]
    public bool AddedCover;

    [ViewVariables, AutoNetworkedField]
    public bool AddedClimbable;

    [ViewVariables, AutoNetworkedField]
    public bool? WasPowerDisabled;

}

[Serializable, NetSerializable]
public enum FlippedStructureVisuals : byte
{
    Tilt,
}
