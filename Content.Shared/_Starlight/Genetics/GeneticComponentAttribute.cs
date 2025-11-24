namespace Content.Shared.Genetics;

/// <summary>
/// Indicates that a component is governed by genetics, can be added or removed
/// by changing a biological entity's DNA, and should be added to the per-round
/// map of DNA regions.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class GeneticComponentAttribute : Attribute
{
    /// <summary>
    /// The number of codons which govern the component, before adding any for
    /// genetics-controlled parameters.
    /// </summary>
    public int Complexity = 2;

    /// <summary>
    /// The stability of the gene - the number of mistakes that you can have,
    /// while still getting the component.
    /// </summary>
    /// <remarks>
    /// Try to keep this under the Complexity
    /// </remarks>
    public int Stability = 1;

    /// <summary>
    /// Construct a new GeneticComponentAttribute
    /// </summary>
    public GeneticComponentAttribute(int complexity = 2, int stability = 1)
    {
        Complexity = complexity;
        Stability = stability;
    }
}
