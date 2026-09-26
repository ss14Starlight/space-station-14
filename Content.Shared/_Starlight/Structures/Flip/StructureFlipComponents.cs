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
    [DataField, AutoNetworkedField]
    public EntProtoId? FlippedPrototype;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan UnflipDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public float CoverChance = 0.45f;

    [DataField]
    public bool DisablePower = true;

    [DataField(customTypeSerializer: typeof(FlagSerializer<CollisionLayer>))]
    public int FlippedLayer = (int)(CollisionGroup.TableLayer | CollisionGroup.BulletImpassable);

    [DataField(customTypeSerializer: typeof(FlagSerializer<CollisionMask>))]
    public int FlippedMask = (int)CollisionGroup.TableMask;

    [DataField]
    public DamageSpecifier? CrushDamage;

    [DataField]
    public TimeSpan CrushKnockdown = TimeSpan.FromSeconds(4);

    [DataField]
    public SoundSpecifier CrushSound = new SoundPathSpecifier("/Audio/Items/Toys/card_tube_bonk.ogg");

    [DataField]
    public float SlipTopplingChance = 0.5f;

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

    [ViewVariables]
    public Dictionary<string, (int Layer, int Mask)> SavedFixtures = new();

    [ViewVariables]
    public bool AddedCover;

    [ViewVariables]
    public bool AddedClimbable;

    [ViewVariables]
    public bool? WasPowerDisabled;

}

[Serializable, NetSerializable]
public enum FlippedStructureVisuals : byte
{
    Tilt,
}
