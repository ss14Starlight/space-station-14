using Robust.Shared.Prototypes;

namespace Content.Shared.Access.Components;

/// <summary>
/// Marks an item as a single-use access chip that can grant accesses to an ID card.
/// </summary>
[RegisterComponent]
public sealed partial class AccessChipComponent : Component
{
    /// <summary>
    /// The list of access levels this chip grants when used on an ID card.
    /// </summary>
    [DataField("grantedAccesses")]
    public List<ProtoId<AccessLevelPrototype>> GrantedAccesses = new();
}
