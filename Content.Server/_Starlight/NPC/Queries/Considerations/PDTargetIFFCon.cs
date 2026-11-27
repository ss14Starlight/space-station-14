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

/// <summary>
/// 1 if the candidate lies close to the NPC and roughly between it and its current "Target",
/// i.e. it could be what is blocking the way. Otherwise 0.
/// Needs a "Target" entity on the blackboard, and scores 0 without one.
/// </summary>
public sealed partial class TargetBlockingCon : UtilityConsideration
{
    /// <summary>How close to the NPC the candidate has to be, in tiles.</summary>
    [DataField("range")]
    public float Range = 1.5f;
 
    /// <summary>
    /// How closely the candidate must lie in the direction of the target.
    /// 1 = dead ahead only, 0 = anywhere in the front half, -1 = no restriction.
    /// </summary>
    [DataField("minDot")]
    public float MinDot = 0.7f;
}