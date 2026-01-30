using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Access.Components;

/// <summary>
///     A component allowing access breakers to add new accesses to configure when breaking a door
/// </summary>
public sealed partial class AccessProviderComponent: Component
{
    /// <summary>
    /// What access groups we want to add
    /// </summary>
    [DataField(readOnly: true)]
    [AutoNetworkedField]
    public HashSet<ProtoId<AccessGroupPrototype>> Groups = new();
}