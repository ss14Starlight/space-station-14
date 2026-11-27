using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.XenoHive.Components;

/// <summary>
/// Lets an entity feed on a downed target over a do-after, apply effects to it, then optionally become something else. Use the LarvaFeedOperator on the HTN logic for it to function.
/// </summary>
[RegisterComponent]
public sealed partial class LarvaFeedComponent : Component
{
    /// <summary>How long the feed takes in seconds.</summary>
    [DataField]
    public TimeSpan FeedTime = TimeSpan.FromSeconds(10);

    [DataField]
    public float DistanceThreshold = 1.5f;

    [DataField]
    public bool BreakOnDamage = true;

    [DataField]
    public bool BreakOnMove = true;

    /// <summary>Mob states a target can be in to be fed on.</summary>
    [DataField]
    public HashSet<MobState> TargetStates = new() { MobState.Critical, MobState.Dead };

    /// <summary>If set, the target must pass this.</summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>If set, dealt to the target once when the feed completes.</summary>
    [DataField]
    public DamageSpecifier? Damage;

    [DataField]
    public bool DamageIgnoreResistances;

    /// <summary>Components added to the target when the feed completes.</summary>
    [DataField]
    public ComponentRegistry AddToTarget = new();

    /// <summary>If set, the target starts rotting after this long.</summary>
    [DataField]
    public TimeSpan? RotAfter;

    /// <summary>If set, spawned at the feeder's position when the feed completes.</summary>
    [DataField]
    public EntProtoId? GrowInto;

    /// <summary>Delete the feeder when the feed completes.</summary>
    [DataField]
    public bool DeleteSelf = true;

    /// <summary>Optional localization ids. Passed "user" and "target".</summary>
    [DataField]
    public LocId? StartPopup;

    [DataField]
    public LocId? DonePopup;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public DoAfterId? DoAfter;
}
