// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;

namespace Content.Shared._Funkystation.Traits.Assorted;

/// <summary>
/// Shared system for Neuroaversion trait logic.
/// Handles seizure build calculations and health-based modifiers.
/// </summary>
public abstract class SharedNeuroAversionSystem : EntitySystem
{
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly MobStateSystem MobState = default!;

    // Seizure build constants (using 0-100 scale instead of 0-1)
    protected const float MinRandomSpikePercent = 0.1f;  // Reduced spike size
    protected const float MaxRandomSpikePercent = 0.5f;  // Reduced spike size
    protected const float RandomSpikeProbabilityBase = 0.001f;  // Less frequent spikes
    protected const float DamageMultiplierMax = 4.0f;

    // Seizure trigger constants
    protected const float SeizureRollInterval = 30f;

    // Trait interaction
    protected const float ChronicMigraineInteractionMultiplier = 1.3f;
    protected const float ChronicMigraineDurationBonus = 2f;

    // Health thresholds
    protected const float BadHealthThreshold = 2f / 3f;
    protected const float OkayHealthThreshold = 1f / 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NeuroAversionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NeuroAversionComponent, ComponentShutdown>(OnShutdown);
    }

    protected virtual void OnStartup(EntityUid uid, NeuroAversionComponent component, ComponentStartup args)
    {
        // Initialize migraine timer
        component.NextMigraineTime = TimeSpan.FromSeconds(Random.NextFloat((float)component.TimeBetweenMigraines.Min.TotalSeconds, (float)component.TimeBetweenMigraines.Max.TotalSeconds));
        component.StartedMindShielded = true;
        Dirty(uid, component);
    }

    protected virtual void OnShutdown(EntityUid uid, NeuroAversionComponent component, ComponentShutdown args)
    {
        // Override in server for cleanup
    }

    /// <summary>
    /// Gets the health condition multiplier based on missing HP fraction.
    /// Combines health state multiplier with mindshield timing severity.
    /// </summary>
    protected static float GetConditionMultiplier(NeuroAversionComponent comp, bool isCritical, float missingHpFrac)
    {
        // Determine base multiplier from health state
        float baseMultiplier = isCritical ? comp.ConditionCriticalMultiplier
            : missingHpFrac >= BadHealthThreshold ? comp.ConditionBadMultiplier
            : missingHpFrac >= OkayHealthThreshold ? comp.ConditionOkayMultiplier
            : comp.ConditionGoodMultiplier;

        // Apply severity multiplier based on when mindshield was applied
        var severityMultiplier = comp.StartedMindShielded
            ? comp.StartedMindShieldedMultiplier
            : comp.MidRoundMindShieldedMultiplier;

        return baseMultiplier * severityMultiplier;
    }

    /// <summary>
    /// Calculates missing HP as a fraction (0-1).
    /// </summary>
    protected static float CalculateMissingHpFraction(DamageableComponent damageable)
    {
        var maxHp = damageable.HealthBarThreshold?.Float() ?? 100f;
        if (maxHp <= 0f)
            return 0f;

        var currentDamage = (float)damageable.TotalDamage;
        return MathF.Max(0f, MathF.Min(1f, currentDamage / maxHp));
    }

    /// <summary>
    /// Updates seizure build based on passive accumulation and random spikes.
    /// Uses 0-100 scale to avoid tiny decimal precision issues.
    /// </summary>
    protected void UpdateSeizureBuild(EntityUid uid, NeuroAversionComponent comp, TimeSpan deltaTime,
        float conditionMultiplier, float traitInteractionMultiplier, float missingHpFrac)
    {
        var seconds = (float)deltaTime.TotalSeconds;

        // Passive build (0-100 scale means we can use nice whole numbers)
        var passiveBuild = comp.BaseSeizurePassivePerSec * conditionMultiplier * traitInteractionMultiplier;
        comp.SeizureBuild += passiveBuild * seconds;

        // Add random spikes - more frequent and larger when damaged
        var spikeProb = RandomSpikeProbabilityBase * seconds * (1f + missingHpFrac * 2f);
        if (Random.NextDouble() < spikeProb)
        {
            var baseSpike = Random.NextFloat(MinRandomSpikePercent, MaxRandomSpikePercent);
            var damageMultiplier = 1f + (missingHpFrac * DamageMultiplierMax);
            comp.SeizureBuild += baseSpike * damageMultiplier;
        }

        // Clamp to valid range
        comp.SeizureBuild = MathF.Max(0f, comp.SeizureBuild);
    }

    /// <summary>
    /// Calculates the chance of a seizure occurring on this roll.
    /// - Rolls happen every 30 seconds.
    /// - For mindshielded: At maximum build, the chance is set so that, on average, a seizure happens once every 5 rounds (20% chance per round, or 0.000929 per roll) for a baseline player.
    /// - For non-mindshielded: At maximum build, the chance is set so that a seizure is almost guaranteed (99.9% chance per round, or 0.0192 per roll).
    /// - The chance scales linearly with build, and is multiplied by both condition and mindshield multipliers.
    /// - For build below 2% of threshold, no seizure can occur.
    ///
    /// Math:
    ///   Let p = per-roll chance at max build for a baseline player.
    ///   (1-p)^(number of rolls per round) = chance of no seizure in a round
    ///   For 240 rolls (2 hours, 30s interval):
    ///     Mindshielded: (1-p)^240 = 0.8  =>  p = 1 - 0.8^(1/240) ≈ 0.000929
    ///     Non-mindshielded: (1-p)^240 = 0.001  =>  p = 1 - 0.001^(1/240) ≈ 0.0192
    ///
    /// Example usage:
    ///   float mindshieldMultiplier = comp.StartedMindShielded ? comp.StartedMindShieldedMultiplier : comp.MidRoundMindShieldedMultiplier;
    ///   float conditionMultiplier = GetConditionMultiplier(...);
    ///   float chance = CalculateSeizureChance(comp, conditionMultiplier, mindshieldMultiplier, isNonMindshielded);
    /// </summary>
    protected static float CalculateSeizureChance(NeuroAversionComponent comp, float conditionMultiplier, float mindshieldMultiplier, bool isNonMindshielded)
    {
        if (comp.SeizureThreshold <= 0f)
            return 0f;

        // Only allow seizure rolls if build is at least 2% of threshold
        if (comp.SeizureBuild < comp.SeizureThreshold * 0.02f)
            return 0f;

        // MaxPerRollChance is the maximum chance per roll at full build.
        // For mindshielded: 0.000929 (20% per round)
        // For non-mindshielded: 0.0192 (99.9% per round)
        float maxPerRollChance = isNonMindshielded ? 0.0192f : 0.000929f;
        float buildFraction = comp.SeizureBuild / comp.SeizureThreshold;
        float chance = buildFraction * maxPerRollChance * conditionMultiplier * mindshieldMultiplier;
        return MathF.Min(1f, chance);
    }

    /// <summary>
    /// Modifies seizure build by the specified amount.
    /// Positive values increase build, negative values decrease.
    /// </summary>
    public void ModifySeizureBuild(EntityUid uid, float amount)
    {
        if (!TryComp<NeuroAversionComponent>(uid, out var comp))
            return;

        comp.SeizureBuild = MathF.Max(0f, MathF.Min(comp.SeizureThreshold * 10f, comp.SeizureBuild + amount));
        Dirty(uid, comp);
    }
}
