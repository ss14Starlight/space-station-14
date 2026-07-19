using Content.Server.Chat.Systems;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Temperature.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Core pathogen infection, progression, immunity, and transmission (contact/ingestion).
/// </summary>
public sealed class PathogenSystem : SharedPathogenSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    /// <summary>
    /// When set, infection rolls use this value instead of RNG (for tests).
    /// </summary>
    public float? ForcedInfectionRoll { get; set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, ContactInteractionEvent>(OnContact);
        SubscribeLocalEvent<SurfaceContaminationComponent, IngestedEvent>(OnContaminatedIngested);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PathogenCarrierComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var carrier, out _))
        {
            var dirty = false;
            for (var i = carrier.Infections.Count - 1; i >= 0; i--)
            {
                var infection = carrier.Infections[i];
                if (infection.NextTick > Timing.CurTime)
                    continue;

                infection.NextTick = Timing.CurTime + TimeSpan.FromSeconds(1);
                if (!TryResolvePathogen(infection.PathogenId, out var pathogen) || pathogen == null)
                {
                    carrier.Infections.RemoveAt(i);
                    dirty = true;
                    continue;
                }

                if (!IsPathogenEnabled(uid, pathogen))
                    continue;

                TickInfection(uid, carrier, infection, pathogen, ref dirty);
            }

            if (dirty)
                Dirty(uid, carrier);
        }

        // Decay surface contamination slowly.
        var surfaceQuery = EntityQueryEnumerator<SurfaceContaminationComponent>();
        while (surfaceQuery.MoveNext(out var uid, out var surface))
        {
            if (surface.Contaminants.Count == 0)
                continue;

            var changed = false;
            for (var i = surface.Contaminants.Count - 1; i >= 0; i--)
            {
                var entry = surface.Contaminants[i];
                if (!TryResolvePathogen(entry.PathogenId, out var pathogen) || pathogen == null)
                {
                    surface.Contaminants.RemoveAt(i);
                    changed = true;
                    continue;
                }

                entry.Load -= pathogen.EnvironmentalDecayPerSecond * frameTime;
                if (entry.Load <= 0.01f)
                {
                    surface.Contaminants.RemoveAt(i);
                    changed = true;
                }
            }

            if (surface.Contaminants.Count == 0 && surface.IsDirty == false)
            {
                // keep dirty flag for surgical tools managed separately
            }

            if (changed)
                Dirty(uid, surface);
        }
    }

    private void OnContact(EntityUid target, MobStateComponent component, ContactInteractionEvent args)
    {
        var source = args.Other;
        if (!IsVirologyEnabledAt(target))
            return;

        if (!TryComp<PathogenCarrierComponent>(source, out var carrier))
        {
            // Contaminated surface/item contact
            if (!TryComp<SurfaceContaminationComponent>(source, out var surface))
                return;

            foreach (var entry in surface.Contaminants)
            {
                TryExpose(target, entry.PathogenId, entry.Load * 0.25f, PathogenTransmission.Contact, source);
            }

            return;
        }

        foreach (var infection in carrier.Infections)
        {
            if (infection.Stage is PathogenStage.Incubation or PathogenStage.Recovering)
                continue;

            if (!TryResolvePathogen(infection.PathogenId, out var pathogen) || pathogen == null)
                continue;

            if ((pathogen.Transmission & PathogenTransmission.Contact) == 0)
                continue;

            TryExpose(target, infection.PathogenId, Math.Max(0.5f, infection.Dose * 0.2f), PathogenTransmission.Contact, source);
        }
    }

    private void OnContaminatedIngested(Entity<SurfaceContaminationComponent> food, ref IngestedEvent args)
    {
        var eater = args.Target;
        if (!IsVirologyEnabledAt(eater))
            return;

        if (!TryGetVirologyStation(eater, out _, out var station) || !station.AllowFoodborne)
            return;

        foreach (var entry in food.Comp.Contaminants)
        {
            TryExpose(eater, entry.PathogenId, entry.Load, PathogenTransmission.Ingestion, food);
        }
    }

    public bool TryExpose(
        EntityUid target,
        string pathogenId,
        float dose,
        PathogenTransmission route,
        EntityUid? source = null,
        bool force = false)
    {
        if (!CanHostPathogens(target))
            return false;

        if (!TryResolvePathogen(pathogenId, out var pathogen) || pathogen == null)
            return false;

        if (!IsPathogenEnabled(target, pathogen) && !force)
            return false;

        if ((pathogen.Transmission & route) == 0 && !force)
            return false;

        if (GetInfection(target, pathogenId) != null)
        {
            // Increase dose on existing infection.
            var carrier = EnsureComp<PathogenCarrierComponent>(target);
            foreach (var infection in carrier.Infections)
            {
                if (infection.PathogenId != pathogenId)
                    continue;
                infection.Dose += dose;
                Dirty(target, carrier);
                return true;
            }
        }

        var chance = pathogen.BaseInfectionChance;
        chance *= GetImmunityMultiplier(target, pathogen);
        chance *= GetPpeCoefficient(target, route);

        // Dose scaling: below infective dose reduces chance.
        if (dose < pathogen.InfectiveDose)
            chance *= dose / pathogen.InfectiveDose;

        if (chance <= 0f && !force)
            return false;

        var rollEv = new PathogenInfectionRollEvent(chance, false);
        if (ForcedInfectionRoll is { } forced)
        {
            rollEv.Infected = forced <= chance;
            rollEv.Handled = true;
        }

        RaiseLocalEvent(target, ref rollEv);

        var infected = rollEv.Handled
            ? rollEv.Infected
            : _random.Prob(Math.Clamp(chance, 0f, 1f));

        if (!infected && !force)
            return false;

        Infect(target, pathogen, Math.Max(dose, pathogen.InfectiveDose), fromSurgery: route == PathogenTransmission.Surgery);
        return true;
    }

    public void Infect(EntityUid target, PathogenPrototype pathogen, float dose, bool fromSurgery = false)
    {
        Infect(target, PathogenDefinition.FromPrototype(pathogen), dose, fromSurgery);
    }

    public void Infect(EntityUid target, PathogenDefinition pathogen, float dose, bool fromSurgery = false)
    {
        if (!CanHostPathogens(target))
            return;

        var carrier = EnsureComp<PathogenCarrierComponent>(target);
        foreach (var existing in carrier.Infections)
        {
            if (existing.PathogenId == pathogen.Id)
            {
                existing.Dose = Math.Max(existing.Dose, dose);
                Dirty(target, carrier);
                return;
            }
        }

        carrier.Infections.Add(new ActivePathogenInfection
        {
            PathogenId = pathogen.Id,
            Dose = dose,
            Stage = PathogenStage.Incubation,
            StageStartedAt = Timing.CurTime,
            NextTick = Timing.CurTime + TimeSpan.FromSeconds(1),
            FromSurgery = fromSurgery,
        });
        Dirty(target, carrier);

        _popup.PopupEntity(Loc.GetString("sol-pathogen-exposed"), target, target);
    }

    /// <summary>
    /// Pathogens are biological. Cyborg chassis and synthetic IPC bodies cannot carry them,
    /// even when exposure is forced by an admin or another system.
    /// </summary>
    public bool CanHostPathogens(EntityUid target)
    {
        return !HasComp<BorgChassisComponent>(target) &&
               !HasComp<IPCBatteryComponent>(target);
    }

    public void Cure(EntityUid target, string pathogenId, bool grantImmunity = true)
    {
        if (!TryComp<PathogenCarrierComponent>(target, out var carrier))
            return;

        for (var i = carrier.Infections.Count - 1; i >= 0; i--)
        {
            if (carrier.Infections[i].PathogenId != pathogenId)
                continue;

            carrier.Infections.RemoveAt(i);
            Dirty(target, carrier);

            if (grantImmunity && TryResolvePathogen(pathogenId, out var pathogen) && pathogen != null)
            {
                var identity = string.IsNullOrEmpty(pathogen.VaccineIdentity) ? pathogen.Id : pathogen.VaccineIdentity;
                GrantImmunity(target, identity, 0f, pathogen.RecoveryImmunityDuration);
            }

            return;
        }
    }

    public bool TryVaccinate(EntityUid target, PathogenVaccineComponent vaccine)
    {
        if (vaccine.Used)
            return false;

        var identity = vaccine.VaccineIdentity;
        if (string.IsNullOrEmpty(identity) && vaccine.PathogenId != null)
            identity = vaccine.PathogenId.Value;

        if (string.IsNullOrEmpty(identity))
            return false;

        // Vaccines do not cure existing symptomatic disease — they prevent acquisition.
        if (TryComp<PathogenCarrierComponent>(target, out var carrier))
        {
            foreach (var infection in carrier.Infections)
            {
                if (infection.PathogenId == identity ||
                    (vaccine.PathogenId != null && infection.PathogenId == vaccine.PathogenId.Value))
                {
                    if (infection.Stage != PathogenStage.Incubation)
                        return false;
                }
            }
        }

        GrantImmunity(target, identity, vaccine.Strength, vaccine.Duration);
        vaccine.Used = true;
        return true;
    }

    private void TickInfection(
        EntityUid uid,
        PathogenCarrierComponent carrier,
        ActivePathogenInfection infection,
        PathogenDefinition pathogen,
        ref bool dirty)
    {
        var elapsed = Timing.CurTime - infection.StageStartedAt;
        var stageDuration = infection.Stage switch
        {
            PathogenStage.Incubation => pathogen.IncubationDuration,
            PathogenStage.Symptomatic => pathogen.SymptomaticDuration,
            PathogenStage.Critical => pathogen.CriticalDuration,
            PathogenStage.Recovering => pathogen.RecoveryDuration,
            _ => TimeSpan.Zero,
        };

        // Treatment chems accelerate recovery / reduce dose.
        TryApplyTreatments(uid, infection, pathogen);

        if (elapsed >= stageDuration)
        {
            AdvanceStage(uid, infection, pathogen, ref dirty);
        }

        switch (infection.Stage)
        {
            case PathogenStage.Symptomatic:
                ApplySymptoms(uid, pathogen, pathogen.SymptomaticDamage);
                ApplyOrganDamage(uid, pathogen);
                break;
            case PathogenStage.Critical:
                ApplySymptoms(uid, pathogen, pathogen.CriticalDamage);
                ApplyOrganDamage(uid, pathogen, multiplier: 1.5f);
                break;
        }

        dirty = true;
    }

    private void AdvanceStage(
        EntityUid uid,
        ActivePathogenInfection infection,
        PathogenDefinition pathogen,
        ref bool dirty)
    {
        switch (infection.Stage)
        {
            case PathogenStage.Incubation:
                infection.Stage = PathogenStage.Symptomatic;
                _popup.PopupEntity(Loc.GetString("sol-pathogen-symptoms-start", ("disease", pathogen.DisplayName)), uid, uid);
                break;
            case PathogenStage.Symptomatic:
                infection.Stage = infection.Dose > pathogen.InfectiveDose * 2f || pathogen.Lethality > 0.5f
                    ? PathogenStage.Critical
                    : PathogenStage.Recovering;
                break;
            case PathogenStage.Critical:
                infection.Stage = PathogenStage.Recovering;
                break;
            case PathogenStage.Recovering:
                Cure(uid, infection.PathogenId);
                _popup.PopupEntity(Loc.GetString("sol-pathogen-recovered", ("disease", pathogen.DisplayName)), uid, uid);
                return;
        }

        infection.StageStartedAt = Timing.CurTime;
        dirty = true;
    }

    private void ApplySymptoms(EntityUid uid, PathogenDefinition pathogen, DamageSpecifier damage)
    {
        if (damage.GetTotal() > 0)
            _damageable.TryChangeDamage(uid, damage, interruptsDoAfters: false);

        if (pathogen.FeverTemperatureOffset != 0 && TryComp<TemperatureComponent>(uid, out var temp))
        {
            // Soft push toward feverish temperature.
            temp.CurrentTemperature += pathogen.FeverTemperatureOffset * 0.05f;
            Dirty(uid, temp);
        }

        if (pathogen.CoughChancePerSecond > 0 && _random.Prob(pathogen.CoughChancePerSecond))
        {
            _chat.TryEmoteWithChat(uid, "Cough", ChatTransmitRange.GhostRangeLimit);
            TryAirborneShed(uid, pathogen, load: 0.5f);
        }

        if (pathogen.SneezeChancePerSecond > 0 && _random.Prob(pathogen.SneezeChancePerSecond))
        {
            _chat.TryEmoteWithChat(uid, "Sneeze", ChatTransmitRange.GhostRangeLimit);
            if ((pathogen.Transmission & PathogenTransmission.Airborne) != 0)
                TryAirborneShed(uid, pathogen, load: 0.35f);
        }
    }

    /// <summary>
    /// Applies organ-targeted damage to installed organs matching slot IDs
    /// (e.g. liver, heart).
    /// </summary>
    public void ApplyOrganDamage(EntityUid uid, PathogenPrototype pathogen, float multiplier = 1f)
    {
        ApplyOrganDamage(uid, PathogenDefinition.FromPrototype(pathogen), multiplier);
    }

    public void ApplyOrganDamage(EntityUid uid, PathogenDefinition pathogen, float multiplier = 1f)
    {
        if (pathogen.OrganDamagePerSecond <= 0f || pathogen.TargetOrgans.Count == 0)
            return;

        if (!HasComp<BodyComponent>(uid))
            return;

        var organDamage = new DamageSpecifier
        {
            DamageDict = new() { { "Poison", pathogen.OrganDamagePerSecond * multiplier } },
        };

        foreach (var (organUid, _) in _body.GetBodyOrgans(uid))
        {
            var protoId = MetaData(organUid).EntityPrototype?.ID ?? string.Empty;
            var name = MetaData(organUid).EntityName;
            var matched = false;
            foreach (var slotId in pathogen.TargetOrgans)
            {
                if (protoId.Contains(slotId, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(slotId, StringComparison.OrdinalIgnoreCase))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
                continue;

            _damageable.TryChangeDamage(organUid, organDamage, interruptsDoAfters: false);
        }
    }

    public void TryAirborneShed(EntityUid uid, PathogenPrototype pathogen, float load)
    {
        TryAirborneShed(uid, PathogenDefinition.FromPrototype(pathogen), load);
    }

    public void TryAirborneShed(EntityUid uid, PathogenDefinition pathogen, float load)
    {
        if ((pathogen.Transmission & PathogenTransmission.Airborne) == 0)
            return;

        if (!TryGetVirologyStation(uid, out _, out var station) || !station.AllowAirborne)
            return;

        // Prefer tile store; keep entity component as a secondary marker for nearby systems.
        EntityManager.System<GridPathogenAtmosphereSystem>().AddAirborneLoad(uid, pathogen.Id, load);

        var airborne = EnsureComp<AirborneContaminantComponent>(uid);
        var found = false;
        foreach (var entry in airborne.Contaminants)
        {
            if (entry.PathogenId != pathogen.Id)
                continue;
            entry.Load += load;
            found = true;
            break;
        }

        if (!found)
        {
            airborne.Contaminants.Add(new PathogenContaminationEntry
            {
                PathogenId = pathogen.Id,
                Load = load,
            });
        }

        Dirty(uid, airborne);
    }

    private const float MinimumTreatmentQuantity = 0.5f;
    private const float TreatmentDoseReduction = 0.5f;

    internal void TryApplyTreatments(EntityUid uid, ActivePathogenInfection infection, PathogenDefinition pathogen)
    {
        if (pathogen.Treatments.Count == 0)
            return;

        // Simplified: if bloodstream contains a treatment reagent, reduce dose.
        // Full metabolizer hooks can refine this later.
        if (!TryComp<Content.Shared.Body.Components.BloodstreamComponent>(uid, out var bloodstream))
            return;

        if (!_solutions.TryGetSolution(uid, bloodstream.BloodSolutionName, out _, out var solution))
            return;

        // Only one treatment application per tick. Trace amounts do not count.
        var hasTreatment = false;
        foreach (var treatment in pathogen.Treatments)
        {
            if (solution.GetTotalPrototypeQuantity(treatment) < MinimumTreatmentQuantity)
                continue;

            hasTreatment = true;
            break;
        }

        if (!hasTreatment)
            return;

        infection.Dose = Math.Max(0, infection.Dose - TreatmentDoseReduction);
        if (infection.Dose < pathogen.InfectiveDose * 0.25f && infection.Stage != PathogenStage.Recovering)
        {
            infection.Stage = PathogenStage.Recovering;
            infection.StageStartedAt = Timing.CurTime;
        }
    }
}
