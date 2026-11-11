// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared._Funkystation.Traits.Assorted;
using Content.Shared.Mindshield.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Popups;
using Content.Shared.Damage.Components;
using Content.Shared.IdentityManagement;
using Content.Server.Mind;
using Content.Server.Jobs;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared._Blimpuf.Traits.Assorted;
using Content.Shared._Starfall.Traits.Assorted;
using Robust.Shared.Timing;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.Traits.Assorted;

/// <summary>
/// Handles Neuroaversion trait. When someone has this trait AND has a mindshield they get migraines (from MigraineSystem) and rare seizures (handled in this system).
/// </summary>
public sealed class NeuroAversionSystem : EntitySystem
{
    [Dependency] private readonly Robust.Shared.Random.IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SeizureSystem _seizure = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private EntityQuery<MindShieldComponent> _mindShieldQuery;
    private EntityQuery<MigraineSchedulerComponent> _chronicMigrainesQuery;

    [Dependency] private readonly IGameTiming _timing = default!;

    // get the passive-build multiplier for the current health tier
    private static float GetConditionMultiplier(NeuroAversionComponent comp, bool isCritical, float missingHpFrac)
    {
        float baseMultiplier;

        if (isCritical)
            baseMultiplier = comp.ConditionCriticalMultiplier;
        else if (missingHpFrac >= 2f / 3f)
            baseMultiplier = comp.ConditionBadMultiplier;
        else if (missingHpFrac >= 1f / 3f)
            baseMultiplier = comp.ConditionOkayMultiplier;
        else
            baseMultiplier = comp.ConditionGoodMultiplier;

        // If the entity is not roundstart mindshielded, they get more severe effects.
        var severityMultiplier = comp.StartedMindShielded
            ? comp.StartedMindShieldedMultiplier
            : comp.MidRoundMindShieldedMultiplier;

        return baseMultiplier * severityMultiplier;
    }

    /// <summary>
    /// Checks if the entity's job prototype (from MindRoleComponent) includes MindShieldComponent or MindShieldImplant.
    /// </summary>
    private bool JobHasMindshieldComponent(EntityUid uid)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindEntity, out var mindComponent))
        {
            return false;
        }

        foreach (var roleUid in mindComponent.MindRoleContainer.ContainedEntities)
        {
            if (!EntityManager.EntityExists(roleUid))
            {
                continue;
            }
            if (!EntityManager.TryGetComponent(roleUid, out MindRoleComponent? role) || role is null)
            {
                continue;
            }
            if (role.JobPrototype is { } jobProtoId)
            {
                if (!_prototypeManager.TryIndex(jobProtoId, out JobPrototype? jobProto) || jobProto == null)
                {
                    continue;
                }
                foreach (var special in jobProto.Special)
                {
                    switch (special)
                    {
                        case AddComponentSpecial addCompSpecial:
                            if (addCompSpecial.Components.ContainsKey("MindShield"))
                            {
                                return true;
                            }
                            break;
                        case AddImplantSpecial implantSpecial:
                            foreach (var implantId in implantSpecial.Implants)
                            {
                                if (implantId == "MindShieldImplant")
                                {
                                    return true;
                                }
                            }
                            break;
                    }
                }
            }
        }
        return false;
    }


    public override void Initialize()
    {
        base.Initialize();

        _mindShieldQuery = GetEntityQuery<MindShieldComponent>();
        _chronicMigrainesQuery = GetEntityQuery<MigraineSchedulerComponent>();
        SubscribeLocalEvent<NeuroAversionComponent, MigrainePopupEvent>(OnMigrainePopup);
        SubscribeLocalEvent<NeuroAversionComponent, MigrainePreventionUpdatedEvent>(OnPreventionUpdated);

        SubscribeLocalEvent<NeuroAversionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NeuroAversionComponent, ComponentShutdown>(OnShutdown);
        // Ideally it would be funny to have a whole system that detects certain keywords a player says negatively about nanotrasen or positively about the syndicate but thats like, severely out of scope of what I want to do and chatcode is scary.
        // This is the only remnant of this system and i only have this here to act as a like "this is what i want done with it" if someone ever wants to implement it properly.
        // please make me able to spam "I HATE NANOTRASEN" to instantly have a seizure
//        SubscribeLocalEvent<NeuroAversionComponent, Content.Server.Chat.Systems.EntitySpokeEvent>(OnEntitySpoke);
    }

    private void OnStartup(EntityUid uid, NeuroAversionComponent component, ComponentStartup args)
    {
        // Initialize migraine timer
        component.NextMigraineTime = _random.NextFloat(component.TimeBetweenMigraines.X, component.TimeBetweenMigraines.Y);
        component.IsMindShielded = _mindShieldQuery.HasComponent(uid);
        component.StartedMindShielded = true;
    }

    private void OnShutdown(EntityUid uid, NeuroAversionComponent component, ComponentShutdown args)
    {
        // Clean up migraine effect
        if (!TerminatingOrDeleted(uid))
        {
            _statusEffects.TryRemoveStatusEffect(uid, MigraineEffectComponent.Prototype);
        }
    }

    private void OnMigrainePopup(EntityUid uid, NeuroAversionComponent component, MigrainePopupEvent args)
    {
        if (!_mindShieldQuery.HasComponent(uid))
            return;

        args.SelfPopup = "blimpuf-neuroaversion-migraine-self";
        args.OthersPopup = "blimpuf-neuroaversion-migraine-others";
    }

    private void OnPreventionUpdated(Entity<NeuroAversionComponent> entity, ref MigrainePreventionUpdatedEvent args)
    {
        entity.Comp.MigrainePreventedUntil = args.ProtectionEnd + args.MinimumDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<NeuroAversionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // dead people cant have migraines or seizures
            if (_mobState.IsDead(uid))
                continue;

            // Mindshield status check and update
            var hasMindShield = _mindShieldQuery.HasComponent(uid);
            if (hasMindShield && !comp.IsMindShielded)
            {
                comp.IsMindShielded = true;
            }
            else if (!hasMindShield && comp.IsMindShielded)
            {
                comp.IsMindShielded = false;
            }

            // Only check and set StartedMindShielded once then NEVER AGAIN!!!!
            if (!comp.StartedMindShieldedChecked)
            {
                comp.StartedMindShielded = comp.IsMindShielded && JobHasMindshieldComponent(uid);
                comp.StartedMindShieldedChecked = true;
            }

            // if they aren't mindshielded fuck off
            if (!comp.IsMindShielded)
                continue;

            // calculate health
            var missingHpFrac = 0f;
            if (TryComp<DamageableComponent>(uid, out var damageable))
            {
                var maxHp = damageable.HealthBarThreshold?.Float() ?? 100f;
                if (maxHp > 0f)
                {
                    var currentDamage = (float)damageable.TotalDamage;
                    missingHpFrac = MathF.Max(0f, MathF.Min(1f, currentDamage / maxHp));
                }
            }

            // Dont allow episodes to trigger if the entity is crit/dead
            // Allow building meter if the entity is in crit however
            if (_mobState.IsDead(uid))
                continue;

            // Decrease next migraine timer faster when more hurt, and apply severity multiplier
            var migraineFactor = 1f - (0.5f * (1f - missingHpFrac));

            // Apply severity multiplier for mid-round mindshielded entities
            var severityMultiplier = comp.StartedMindShielded
                ? comp.StartedMindShieldedMultiplier
                : comp.MidRoundMindShieldedMultiplier;

            if (!_statusEffects.HasStatusEffect(uid, MigraineEffectComponent.Prototype) &&
                !_statusEffects.HasStatusEffect(uid, MigrainePreventionEffectComponent.Prototype))
            {
                comp.NextMigraineTime -= frameTime * (1f / migraineFactor) * severityMultiplier;
                if (comp.NextMigraineTime <= 0f && _timing.CurTime >= comp.MigrainePreventedUntil)
                {
                    // Reset next migraine timer. Typical configured timings are 8-20 minutes (480-1200s).
                    // TimeBetweenMigraines should be set to something like (480f, 1200f) for that behavior.
                    comp.NextMigraineTime = _random.NextFloat(comp.TimeBetweenMigraines.X, comp.TimeBetweenMigraines.Y);
                    var duration = _random.NextFloat(comp.MigraineDuration.X, comp.MigraineDuration.Y);
                    TriggerMigraine(uid, duration);
                }
            }

            // Seizure meter: passive build accumulates over time and is scaled by condition tiers
            // (Good/Okay/Bad/Critical) derived from missing HP fraction.
            // Determine condition multiplier from missingHpFrac
            var conditionMult = GetConditionMultiplier(comp, _mobState.IsCritical(uid), missingHpFrac);

            // Check for trait interaction with Chronic Migraines
            var hasChronicMigraines = _chronicMigrainesQuery.HasComponent(uid);
            var traitInteractionMultiplier = hasChronicMigraines ? 1.3f : 1.0f; // 30% extra build with both traits

            // Passive build
            var passive = comp.BaseSeizurePassivePerSec * conditionMult * traitInteractionMultiplier;

            comp.SeizureBuild += passive * frameTime;

            // Add random jumps to make seizures feel truly random but still health-state dependent
            // More frequent spikes but smaller when healthy, larger when damaged
            var randomSpikeProbability = 0.005f * frameTime * (1f + missingHpFrac * 2f);
            if (_random.NextDouble() < randomSpikeProbability)
            {
                // Smaller spikes in good condition, escalating with damage
                var baseSpike = _random.NextFloat(0.005f, 0.02f); // 0.5-2% base spike
                var damageMultiplier = 1f + (missingHpFrac * 4f); // 1x to 5x multiplier based on damage
                var randomSpike = baseSpike * damageMultiplier;
                comp.SeizureBuild += randomSpike;
            }

            // CLAMP THAT BITCH
            if (comp.SeizureBuild < 0f)
                comp.SeizureBuild = 0f;

            // Handle seizure triggering, rolls every 3 seconds
            // Only attempt to trigger if not dead, not in crit, and not already having a seizure
            if (!_mobState.IsCritical(uid) && !_seizure.IsSeizing(uid))
            {
                comp.NextSeizureRollTime -= frameTime;

                if (comp.NextSeizureRollTime <= 0f)
                {
                    // Reset timer for next roll
                    comp.NextSeizureRollTime = 3f;

                    var buildFraction = 0f;
                    if (comp.SeizureThreshold > 0f)
                        buildFraction = MathF.Max(0f, MathF.Min(1f, comp.SeizureBuild / comp.SeizureThreshold));

                    // Calculate seizure chance as a percentage

                    // Chance based on build level with exponential scaling for dramatic effect
                    var scaledBuildFraction = buildFraction * buildFraction * buildFraction;
                    var buildChancePercent = 25f * scaledBuildFraction; // Up to 25% at full build

                    // Health condition multiplier for build-based chance
                    var seizureConditionMult = GetConditionMultiplier(comp, _mobState.IsCritical(uid), missingHpFrac);
                    var totalChancePercent = buildChancePercent * seizureConditionMult;
                    var chanceDecimal = totalChancePercent / 100f; // Convert to 0-1 range

                    // Roll for seizure
                    var roll = _random.NextDouble();
                    var hit = roll < chanceDecimal;

                    if (hit)
                    {
                        // Trigger seizure and reset build
                        _seizure.StartSeizure(uid, null, 10f);
                        comp.SeizureBuild = comp.PostSeizureResidual;
                    }
                }
            }
        }
    }

    private void TriggerMigraine(EntityUid uid, float duration)
    {
        if (_chronicMigrainesQuery.HasComponent(uid))
            duration += 2f;

        _statusEffects.TrySetStatusEffectDuration(uid, MigraineEffectComponent.Prototype, TimeSpan.FromSeconds(duration));
    }

    /// <summary>
    /// Modifies seizure build on the target entity by <paramref name="amount"/>.
    /// Positive values increase build, negative values decrease build.
    /// </summary>
    public void ModifySeizureBuild(EntityUid uid, float amount)
    {
        if (!TryComp<NeuroAversionComponent>(uid, out var comp))
            return;

        var newBuild = comp.SeizureBuild + amount;
        comp.SeizureBuild = MathF.Max(0f, MathF.Min(comp.SeizureThreshold * 10f, newBuild));
    }


    /// <summary>
    /// Attempts to trigger a seizure on the target entity.
    /// Returns true if the target has a <see cref="NeuroAversionComponent"/> and the action was performed.
    /// </summary>
    public bool TryTriggerSeizure(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        if (!TryComp<NeuroAversionComponent>(uid, out var comp))
            return false;

        // Trigger seizure and reset build
        _seizure.StartSeizure(uid);
        comp.SeizureBuild = comp.PostSeizureResidual;
        return true;
    }
}
