namespace Content.Shared.Genetics;

/// <summary>
/// Marks a field or property on a <see cref="GeneticComponentAttribute"/>-annotated component
/// as having its value encoded into the entity's DNA.
/// </summary>
/// <remarks>
/// <para>
/// The values array lists the discrete values from worst (0 codons match the
/// canonical sequence) to best (all codons match). The number of additional
/// codons consumed is <c>values.Length - 1</c>.
/// </para>
/// <para>
/// When the DNA changes in this variable's codon region, the field is updated
/// to the value corresponding to the number of matching codons. When the field
/// is changed externally, the DNA is updated to the closest representable value
/// but the field itself is NOT clamped.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the field (e.g. <see langword="float"/>).</typeparam>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class GeneticMultiValueVariableAttribute<T> : Attribute
{
    /// <summary>
    /// The default value for the field when the component exists but isn't
    /// under genetic control (e.g. added by prototype, not by DNA).
    /// </summary>
    public T DefaultValue { get; }

    /// <summary>
    /// Discrete value steps from worst (index 0 = no codons match) to best
    /// (index N = all codons match).
    /// </summary>
    public T[] Values { get; }

    public GeneticMultiValueVariableAttribute(T defaultValue, params T[] values)
    {
        DefaultValue = defaultValue;
        Values = values;
    }
}
