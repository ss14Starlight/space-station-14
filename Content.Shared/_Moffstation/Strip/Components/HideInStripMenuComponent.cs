using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Strip.Components;

/// <summary>
/// This component indicates that the entity should be hidden in the stip menu.
/// </summary>
[RegisterComponent, AutoGenerateComponentState]
[Access] // Presently readonly, but there's no reason the strategy couldn't change dynamically.
public sealed partial class HideInStripMenuComponent : Component
{
    /// <summary>
    /// The definition of how this entity should be hidden in the strip menu.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public HideInStripMenuStrategy Strategy = default!;
}

/// <summary>
/// This class is the supertype of strategies describing how <see cref="HideInStripMenuComponent"/> should hide entities.
/// </summary>
[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public abstract partial class HideInStripMenuStrategy;

/// <summary>
/// This <see cref="HideInStripMenuStrategy"/> hides an entity in the strip menu by replacing it with a virtual instance
/// of <see cref="Prototype"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class HideInStripMenuWithEntityStrategy : HideInStripMenuStrategy
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}

/// <summary>
/// This <see cref="HideInStripMenuStrategy"/> hides an entity in the strip menu by replacing it with an entity which is
/// "synthesized" from the necessary parts herein.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class HideInStripMenuWithSyntheticEntityStrategy : HideInStripMenuStrategy
{
    [DataField(required: true)]
    public PrototypeLayerData[] Sprite = [];

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;
}

/// <summary>
/// This <see cref="HideInStripMenuStrategy"/> indicates that virtual entity creation is delegated to some other system
/// by firing <see cref="GetHideInStripMenuEntityEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class HideInStripMenuWithDynamicStrategy : HideInStripMenuStrategy;

/// <summary>
/// This event is raised on an entity whose <see cref="HideInStripMenuComponent.Strategy"/> is
/// <see cref="HideInStripMenuWithDynamicStrategy"/>. Handlers of this even can spawn an entity (at
/// <see cref="SpawnAt"/>) which will be used as the replacement entity in the strip menu. The strippable system will
/// delete the entity when it's not needed, so handlers need not worry about that. Handlers may leave
/// <see cref="Entity"/> as null, indicating the entity should actually not be hidden.
/// </summary>
[ByRefEvent]
public struct GetHideInStripMenuEntityEvent(MapCoordinates spawnAt)
{
    public EntityUid? Entity;
    public readonly MapCoordinates SpawnAt = spawnAt;
}
