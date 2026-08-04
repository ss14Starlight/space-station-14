using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics.Components;

/// <summary>
/// The component for a gene sampler. Contrary to the name, a sampler can both extract and inject genes. Used in combination with a genetics manipulation console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneSamplerComponent : Component
{
     /*\
    | * | Should this be split into two components? Actually three since GeneSample and GeneInjector would need to share genes, which would best be done in a third ContainedGenesComponent.
    | * | Arguably yes but I'm really not in the mood when it would only create two completely blank components that do nothing except signal to the system which mode the relevant object is in.
    | * | And that can be accomplished with a simple boolean.
    | * | So if you think such a rewrite would be beneficial, go do it, I'm not going to stop you. But at least realize the absurdity. Also you can't make me do it.
    | * | I believe it was George Orwell who said "Break any of these [writing] rules sooner than say anything outright barbarous".
     \*/

    /// <summary>
    /// If true, will extract a gene sample from a target. If false, will insert a gene sample, overriding its current genome.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsCurrentlySampling = true;

    /// <summary>
    /// The genes in this sampler.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public HashSet<EntityUid> Genes = new();
}

[Serializable, NetSerializable]
public enum GeneSamplerVisuals : byte
{
    Signal
}
