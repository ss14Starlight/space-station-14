using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Queries.Queries;

/// <summary>
/// Returns nearby components that match the specified components.
/// </summary>
public sealed partial class ComponentQuery : UtilityQuery
{
    [DataField("components", required: true)]
    public ComponentRegistry Components = default!;
}

/// Persistence Start
/// <summary>
/// Persistence: Returns nearby entities that match any of the specified components
/// </summary>
public sealed partial class ComponentQueryAny : UtilityQuery
{
    [DataField("components", required: true)]
    public ComponentRegistry Components = default!;
}
/// Persistence End
