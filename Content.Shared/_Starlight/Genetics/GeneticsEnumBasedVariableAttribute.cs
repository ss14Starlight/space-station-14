namespace Content.Shared.Genetics;

/// <summary>
/// Marks a field or property on a <see cref="GeneticComponentAttribute"/>-annotated component
/// as having its value selected from a set of discrete choices, each with its own complexity
/// and stability score encoded in the entity's DNA.
/// </summary>
/// <remarks>
/// <para>
/// Use together with one or more <see cref="GeneticsEnumEntryAttribute"/> instances on the
/// same member to define the possible values.
/// </para>
/// <para>
/// The getter and setter method names refer to methods on the component class that translate
/// between a primitive <see langword="string"/> key and the actual (possibly complex) field
/// value. This keeps attribute parameters simple while supporting arbitrarily complex types.
/// </para>
/// <para>
/// The getter should return <see langword="null"/> when the field holds the default value
/// (i.e., no entry matches). The setter receives <see langword="null"/> when the genetics
/// system wants to reset the field to its default.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class GeneticsEnumBasedVariableAttribute : Attribute
{
    /// <summary>
    /// Name of a <c>public string? MethodName()</c> method on the component that returns
    /// the string key corresponding to the field's current value, or <see langword="null"/>
    /// for the default.
    /// </summary>
    public string GetterMethod { get; }

    /// <summary>
    /// Name of a <c>public void MethodName(string? key)</c> method on the component that
    /// sets the field to the value corresponding to the given key, or the default when
    /// <see langword="null"/>.
    /// </summary>
    public string SetterMethod { get; }

    public GeneticsEnumBasedVariableAttribute(string getterMethod, string setterMethod)
    {
        GetterMethod = getterMethod;
        SetterMethod = setterMethod;
    }
}
