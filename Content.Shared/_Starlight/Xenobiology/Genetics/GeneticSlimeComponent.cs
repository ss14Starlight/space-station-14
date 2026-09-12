using Content.Shared._Starlight.Genetics;
using Content.Shared._Starlight.Genetics.Genes.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneticSlimeComponent : Component
{
    /// <summary>
    /// How many slimes the parent is split into when reproducing.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    [AutoNetworkedField]
    public int SplitAmount = 4;

    /// <summary>
    /// The amount of nutrition gained after biting a target.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    [AutoNetworkedField]
    public float BiteNutritionGain = 10.0f;

    /// <summary>
    /// The next entity this slime creates when splitting.
    /// </summary>
    [DataField(required: true)]
    [AutoNetworkedField]
    public EntProtoId SplitEntity = default;

    /// <summary>
    /// The extract this slime spawns when processed.
    /// </summary>
    [DataField(required: true)]
    [AutoNetworkedField]
    public EntProtoId ExtractEntity = default;

    [ViewVariables(VVAccess.ReadOnly)]
    [DataField]
    [AutoNetworkedField]
    public HashSet<ProtoId<SampleGenePrototype>> StartingGeneIDs = new();

    [ViewVariables]
    [AutoNetworkedField]
    public bool ShouldAddStarters = true;
}
