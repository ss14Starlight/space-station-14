using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Hitscan entities that have this component will do additional effects to targets that are able to crawl.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanCrawlerTargetEffectsComponent : Component
{
    /// <summary>
    /// How long the hitscan weapon will stun a target.
    /// </summary>
    [DataField]
    public TimeSpan StunDuration = TimeSpan.Zero;

    /// <summary>
    /// How long the hitscan weapon will knock down a target.
    /// </summary>
    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.Zero;

    /// <summary>
    /// How long the hitscan weapon will slow a target.
    /// </summary>
    [DataField]
    public TimeSpan SlowDuration = TimeSpan.Zero;

    /// <summary>
    /// During the slow from this weapon, how much walking will be modified.
    /// </summary>
    [DataField]
    public float WalkSpeedMultiplier = 1f;

    /// <summary>
    /// During the slow from this weapon, how much running will be modified.
    /// </summary>
    [DataField]
    public float RunSpeedMultiplier = 1f;
}
