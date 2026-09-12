using Content.Shared._Starlight.Genetics.GeneticTraits.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Genetics.Genes.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GenesComponent : Component
{
    /// <summary>
    /// The list of genes that this entity currently has.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public HashSet<EntityUid> Genes = new();

    /// <summary>
    /// The traits that can be pulled from to make a gene.
    /// </summary>
    [ViewVariables, AutoNetworkedField, DataField(required: true)]
    public HashSet<ProtoId<GeneticTraitPrototype>> AvailableTraits;

    /// <summary>
    /// The classes of traits this genome can apply to an entity. If a trait has one of the classes in this set, it can
    /// be applied to the entity. See also <see cref="AbstractTraitPrototype"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField, DataField(required: true)]
    public HashSet<string> Classes;
}
