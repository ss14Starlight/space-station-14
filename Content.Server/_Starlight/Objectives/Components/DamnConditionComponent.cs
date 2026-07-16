using Content.Server._Starlight.Objectives.Systems;
using Content.Shared._Starlight.Devil;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Objectives.Components;

/// <summary>
/// Requires the owner to damn a set number of people
/// </summary>
[RegisterComponent, Access(typeof(DamnConditionSystem))]
public sealed partial class DamnConditionComponent : Component
{
    /// <summary>
    /// Number that needs to be reached for objective completion
    /// </summary>
    [DataField]
    public int Amount = 7;

    /// <summary>
    /// Do we just require the entity to be damned, or do we require a specific curse
    /// </summary>
    [DataField]
    public bool RequireSpecificDamnations = true;

    /// <summary>
    /// If so, what damnations are required
    /// </summary>
    [DataField]
    public List<ProtoId<DamnationPrototype>> RequiredDamnations = [
        "Soul"
    ];

    [DataField(required: true)]
    public LocId DescriptionText;
}
