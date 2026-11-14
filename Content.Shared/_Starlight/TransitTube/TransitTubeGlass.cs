using Content.Shared.Damage.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.TransitTube;

[Prototype]
public sealed partial class TransitTubeMaterialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Tint of the glass layer.
    /// </summary>
    [DataField]
    public Color Color = Color.White;

    /// <summary>
    /// The sheets used on a tube to reglaze it with this material.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<StackPrototype> Stack;

    [DataField]
    public int Cost = 1;

    /// <summary>
    /// Left behind when a tube of this material is smashed.
    /// </summary>
    [DataField]
    public EntProtoId? Shard;

    /// <summary>
    /// Applied to damage the tube takes, on top of its own resistances.
    /// </summary>
    [DataField]
    public ProtoId<DamageModifierSetPrototype>? Modifiers;

    /// <summary>
    /// How much of the heat outside (hot gas, lava) the tube keeps off the pods inside, from 0 to 1.
    /// </summary>
    [DataField]
    public float HeatInsulation;
}

/// <summary>
/// The glass a transit tube is made of. Use sheets of another material on the tube to reglaze it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class TransitTubeGlassComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<TransitTubeMaterialPrototype> Material = "TransitTubeReinforcedGlass";
}
