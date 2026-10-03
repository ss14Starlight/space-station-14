using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Roles;

[DataDefinition]
public partial struct OrganEntry
{
    /// <summary>
    /// The prototype ID of the organ/implant entity.
    /// </summary>
    [DataField("proto", required: true)]
    public EntProtoId Proto { get; set; } = default!;

    /// <summary>
    /// Whether to replace any existing organ in this slot before equipping.
    /// </summary>
    [DataField("replace")]
    public bool Replace { get; set; } = false;

    public OrganEntry()
    {
    }
}