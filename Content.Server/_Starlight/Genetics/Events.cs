using Robust.Shared.Serialization;

namespace Content.Server.Genetics;

// Starlight start
/// <summary>
/// Raised on an entity to generate its DNA string.
/// </summary>
[ByRefEvent]
public record struct ConstructDnaEvent()
{
    /// <summary>
    /// The entity constructing its DNA.
    /// </summary>
    public required EntityUid Owner;

    /// <summary>
    /// The constructed DNA string.
    /// </summary>
    public string? DNA;
}
// Starlight end
