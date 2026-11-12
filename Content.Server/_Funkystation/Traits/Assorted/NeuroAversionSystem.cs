// SPDX-FileCopyrightText: 2026 Mora <46364955+TrixxedHeart@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server.Jobs;
using Content.Server.Mind;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Roles.Components;
using Content.Shared._Blimpuf.Traits.Assorted;
using Content.Shared._Starfall.Traits.Assorted;
using Robust.Shared.Timing;
using Content.Shared._Funkystation.Traits.Assorted;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.Traits.Assorted;

/// <summary>
/// Server-side system for Neuroaversion trait.
/// Handles migraines, seizures, and mindshield interactions.
/// </summary>
public sealed class NeuroAversionSystem : SharedNeuroAversionSystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SeizureSystem _seizure = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private EntityQuery<MindShieldComponent> _mindShieldQuery;
    private EntityQuery<MigraineSchedulerComponent> _chronicMigrainesQuery;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        _mindShieldQuery = GetEntityQuery<MindShieldComponent>();
        _chronicMigrainesQuery = GetEntityQuery<MigraineSchedulerComponent>();
        SubscribeLocalEvent<NeuroAversionComponent, MigrainePopupEvent>(OnMigrainePopup);
        SubscribeLocalEvent<NeuroAversionComponent, MigrainePreventionUpdatedEvent>(OnPreventionUpdated);
    }

    protected override void OnShutdown(EntityUid uid, NeuroAversionComponent component, ComponentShutdown args)
    {
        base.OnShutdown(uid, component, args);
        if (!TerminatingOrDeleted(uid))
            _statusEffects.TryRemoveStatusEffect(uid, MigraineEffectComponent.Prototype);
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

        var deltaTime = TimeSpan.FromSeconds(frameTime);

        var query = EntityQueryEnumerator<NeuroAversionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Dead entities don't have migraines or seizures
            if (MobState.IsDead(uid))
                continue;

            UpdateMindshieldStatus(uid, comp);

            comp.SeizurePaused = _solutions.TryGetSolution(uid, "bloodstream", out _, out var bloodstream)
                                 && bloodstream.ContainsReagent(new ReagentId("Psicodine", null));

            // Only process if mindshielded
            if (!comp.IsMindShielded)
            {
                Dirty(uid, comp);
                continue;
            }

            var missingHpFrac = CalculateHealthFraction(uid);

            UpdateMigraineTimer(uid, comp, frameTime, missingHpFrac);
            UpdateSeizureLogic(uid, comp, deltaTime, missingHpFrac);
            Dirty(uid, comp);
        }
    }

    private void UpdateMindshieldStatus(EntityUid uid, NeuroAversionComponent comp)
    {
        var hasMindShield = _mindShieldQuery.HasComponent(uid);
        comp.IsMindShielded = hasMindShield;
        // Check if started mindshielded only once
        if (!comp.StartedMindShieldedChecked)
        {
            comp.StartedMindShielded = comp.IsMindShielded && JobHasMindshieldComponent(uid);
            comp.StartedMindShieldedChecked = true;
        }
    }

    private float CalculateHealthFraction(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return 0f;

        return CalculateMissingHpFraction(damageable);
    }

    private void UpdateMigraineTimer(EntityUid uid, NeuroAversionComponent comp, float frameTime, float missingHpFrac)
    {
        if (_statusEffects.HasStatusEffect(uid, MigraineEffectComponent.Prototype) ||
            _statusEffects.HasStatusEffect(uid, MigrainePreventionEffectComponent.Prototype))
            return;

        // Migraines trigger faster when more damaged
        var migraineFactor = 1f - (0.5f * missingHpFrac);
        var severityMultiplier = comp.StartedMindShielded
            ? comp.StartedMindShieldedMultiplier
            : comp.MidRoundMindShieldedMultiplier;

        var seconds = frameTime * (1f / migraineFactor) * severityMultiplier;
        comp.NextMigraineTime -= TimeSpan.FromSeconds(seconds);

        if (comp.NextMigraineTime <= TimeSpan.Zero && _timing.CurTime >= comp.MigrainePreventedUntil)
        {
            // Pick new migraine time
            var nextMigraineSeconds = Random.NextFloat((float)comp.TimeBetweenMigraines.Min.TotalSeconds, (float)comp.TimeBetweenMigraines.Max.TotalSeconds);
            comp.NextMigraineTime = TimeSpan.FromSeconds(nextMigraineSeconds);
            var durationSeconds = Random.NextFloat((float)comp.MigraineDuration.Min.TotalSeconds, (float)comp.MigraineDuration.Max.TotalSeconds);
            TriggerMigraine(uid, durationSeconds);
        }
    }

    private void UpdateSeizureLogic(EntityUid uid, NeuroAversionComponent comp, TimeSpan deltaTime, float missingHpFrac)
    {
        var seconds = (float) deltaTime.TotalSeconds;
        if (seconds <= 0f)
            return;

        var isCritical = MobState.IsCritical(uid);
        var conditionMult = GetConditionMultiplier(comp, isCritical, missingHpFrac);

        // Check for chronic migraines trait interaction
        var hasChronicMigraines = _chronicMigrainesQuery.HasComponent(uid);
        var traitInteractionMult = hasChronicMigraines
            ? ChronicMigraineInteractionMultiplier
            : 1f;

        if (!comp.SeizurePaused)
        {
            UpdateSeizureBuild(uid, comp, deltaTime, conditionMult, traitInteractionMult, missingHpFrac);
        }

        // No seizures while critical, already seizing, or paused
        if (isCritical || _seizure.IsSeizing(uid) || comp.SeizurePaused)
            return;

        // Countdown to next hazard check
        comp.NextSeizureCheckTime -= deltaTime;
        if (comp.NextSeizureCheckTime > TimeSpan.Zero)
            return;

        comp.NextSeizureCheckTime = comp.SeizureCheckInterval;

        // Mindshield severity
        float mindshieldMult = comp.StartedMindShielded
            ? comp.StartedMindShieldedMultiplier
            : comp.MidRoundMindShieldedMultiplier;

        // Calculate hazard (per second)
        float hazard =
            (comp.SeizureBuild * comp.BuildHazardFactor);

        hazard *= conditionMult * traitInteractionMult;

        float interval = (float)comp.SeizureCheckInterval.TotalSeconds;
        float probability = 1f - MathF.Exp(-hazard * interval);

        double roll = Random.NextDouble();
        bool triggered = roll < probability;
        if (triggered)
        {
            // Use the effects system to trigger a seizure
            var effect = new SeizureSystem.TriggerSeizureEffect { SeizureDuration = Random.NextFloat(10f, 30f) };
            _entityEffects.ApplyEffect(uid, effect);
        }
    }

    private void TriggerMigraine(EntityUid uid, float duration)
    {
        // Extend duration if chronic migraines present
        if (_chronicMigrainesQuery.HasComponent(uid))
        {
            duration += ChronicMigraineDurationBonus;
        }

        _statusEffects.TrySetStatusEffectDuration(uid, MigraineEffectComponent.Prototype, TimeSpan.FromSeconds(duration));
    }

    /// <summary>
    /// Checks if the entity's job includes mindshield component or implant.
    /// </summary>
    private bool JobHasMindshieldComponent(EntityUid uid)
    {
        if (!_mindSystem.TryGetMind(uid, out _, out var mindComponent))
            return false;

        foreach (var roleUid in mindComponent.MindRoleContainer.ContainedEntities)
        {
            if (!EntityManager.EntityExists(roleUid) ||
                !EntityManager.TryGetComponent(roleUid, out MindRoleComponent? role) ||
                role.JobPrototype is not { } jobProtoId ||
                !_prototypeManager.TryIndex(jobProtoId, out JobPrototype? jobProto))
                continue;

            foreach (var special in jobProto.Special)
            {
                // We check for both AddComponentSpecial and AddImplantSpecial because while they're ONLY supposed to be given via AddImplantSpecial I don't want the scenario where for some fucking reason someone decides to use AddComponentSpecial to do it instead.
                if ((special is AddComponentSpecial addCompSpecial && addCompSpecial.Components.ContainsKey("MindShield")) ||
                    (special is AddImplantSpecial implantSpecial && implantSpecial.Implants.Any(id => id == "MindShieldImplant")))
                    return true;
            }
        }

        return false;
    }
}
