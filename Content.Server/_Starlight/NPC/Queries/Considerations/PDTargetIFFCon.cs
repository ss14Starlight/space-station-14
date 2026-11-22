using Content.Server.NPC.Queries.Considerations;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f if the target is not shot from the same grid or 0f if its is.
/// </summary>
public sealed partial class PDTargetIFFCon : UtilityConsideration;

/// <summary>
/// 1 if the target is a member of the given NPC faction, otherwise 0.
/// </summary>
public sealed partial class TargetInFactionCon : UtilityConsideration
{
    [DataField("faction", required: true)]
    public ProtoId<NpcFactionPrototype> Faction;
}

/// <summary>
/// 1 if the target is in combat mode, otherwise 0.
/// </summary>
public sealed partial class TargetInCombatModeCon : UtilityConsideration
{
}