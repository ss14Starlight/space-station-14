using Content.Shared._Starlight.Mindshield.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Revolutionary.Components;
using Content.Shared.StatusEffect;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Mindshield;

/// <summary>
/// Shared system for handling mindshield degradation for head revolutionaries.
/// </summary>
public abstract class SharedMindshieldDegradationSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly StatusEffectsSystem StatusEffects = default!;

    public const string MindshieldDegradingStatusEffect = "MindshieldDegrading";

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<MindshieldDegradationComponent, ComponentRemove>(OnDegradationRemoved);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentAdd>(OnHeadRevAdded);
    }

    /// <summary>
    /// When someone becomes a head revolutionary, check if they have a mindshield and start degradation
    /// </summary>
    private void OnHeadRevAdded(EntityUid uid, HeadRevolutionaryComponent component, ComponentAdd args)
    {
        if (HasComp<MindShieldComponent>(uid) && !HasComp<MindshieldDegradationComponent>(uid))
        {
            StartMindshieldDegradation(uid);
        }
    }

    /// <summary>
    /// Clean up status effects when degradation component is removed
    /// </summary>
    private void OnDegradationRemoved(EntityUid uid, MindshieldDegradationComponent component, ComponentRemove args)
    {
        StatusEffects.TryRemoveStatusEffect(uid, MindshieldDegradingStatusEffect);
    }

    /// <summary>
    /// Starts the mindshield degradation process for a head revolutionary
    /// </summary>
    public virtual void StartMindshieldDegradation(EntityUid uid)
    {
        if (!HasComp<MindShieldComponent>(uid) || !HasComp<HeadRevolutionaryComponent>(uid))
            return;

        var degradation = EnsureComp<MindshieldDegradationComponent>(uid);
        degradation.StartTime = Timing.CurTime;
        degradation.WarningShown = false;
        degradation.DegradationComplete = false;

        // Add status effect for the full duration
        StatusEffects.TryAddStatusEffect(uid, MindshieldDegradingStatusEffect, degradation.DegradationTime, true);
        
        Dirty(uid, degradation);
    }

    /// <summary>
    /// Gets the current degradation progress (0.0 to 1.0)
    /// </summary>
    public float GetDegradationProgress(EntityUid uid, MindshieldDegradationComponent? degradation = null)
    {
        if (!Resolve(uid, ref degradation, false))
            return 0f;

        var elapsed = Timing.CurTime - degradation.StartTime;
        var progress = (float)(elapsed.TotalSeconds / degradation.DegradationTime.TotalSeconds);
        return Math.Clamp(progress, 0f, 1f);
    }

    /// <summary>
    /// Gets the remaining time until mindshield destruction
    /// </summary>
    public TimeSpan GetRemainingTime(EntityUid uid, MindshieldDegradationComponent? degradation = null)
    {
        if (!Resolve(uid, ref degradation, false))
            return TimeSpan.Zero;

        var elapsed = Timing.CurTime - degradation.StartTime;
        var remaining = degradation.DegradationTime - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Checks if the warning should be shown (at 5 minutes)
    /// </summary>
    public bool ShouldShowWarning(EntityUid uid, MindshieldDegradationComponent? degradation = null)
    {
        if (!Resolve(uid, ref degradation, false))
            return false;

        if (degradation.WarningShown)
            return false;

        var elapsed = Timing.CurTime - degradation.StartTime;
        return elapsed >= degradation.WarningTime;
    }

    /// <summary>
    /// Checks if the degradation is complete (at 10 minutes)
    /// </summary>
    public bool IsDegradationComplete(EntityUid uid, MindshieldDegradationComponent? degradation = null)
    {
        if (!Resolve(uid, ref degradation, false))
            return false;

        var elapsed = Timing.CurTime - degradation.StartTime;
        return elapsed >= degradation.DegradationTime;
    }
}
