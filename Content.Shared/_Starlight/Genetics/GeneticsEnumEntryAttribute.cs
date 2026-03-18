namespace Content.Shared.Genetics;

/// <summary>
/// Defines one possible value for a <see cref="GeneticsEnumBasedVariableAttribute"/>-annotated field.
/// Each entry has its own canonical DNA sub-sequence, complexity, and stability.
/// </summary>
/// <remarks>
/// <para>
/// The variable region in the DNA is sized to <c>max(Complexity + Stability)</c> across all
/// entries on the same member. Each entry gets its own per-round random canonical sequence.
/// </para>
/// <para>
/// When reading DNA, the entry whose canonical best matches (within its stability threshold)
/// is selected. Ties are broken by highest complexity, then highest stability.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class GeneticsEnumEntryAttribute : Attribute
{
    /// <summary>
    /// The number of codons that must match for this entry to be selected.
    /// Higher complexity entries are harder to achieve but win tiebreakers.
    /// </summary>
    public int Complexity { get; }

    /// <summary>
    /// The number of allowed mismatches within the <c>Complexity + Stability</c>
    /// codon window. Higher stability makes the entry easier to match.
    /// </summary>
    public int Stability { get; }

    /// <summary>
    /// The string key that identifies this entry. Passed to the setter method
    /// defined by <see cref="GeneticsEnumBasedVariableAttribute"/> when this
    /// entry is selected from DNA, and returned by the getter method when the
    /// field currently holds this entry's value.
    /// </summary>
    public string Key { get; }

    public GeneticsEnumEntryAttribute(int complexity, int stability, string key)
    {
        Complexity = complexity;
        Stability = stability;
        Key = key;
    }
}
