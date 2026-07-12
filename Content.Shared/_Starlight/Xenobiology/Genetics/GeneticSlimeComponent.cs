using Robust.Shared.GameStates;

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
}
