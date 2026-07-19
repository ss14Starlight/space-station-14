using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Swab collection, disease diagnoser, vaccinator, and vaccine application.
/// </summary>
public sealed class DiseaseEquipmentSystem : EntitySystem
{
    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DiseasePathogenSwabComponent, AfterInteractEvent>(OnSwabInteract);
        SubscribeLocalEvent<DiseaseDiagnoserComponent, AfterInteractUsingEvent>(OnDiagnoserInsert);
        SubscribeLocalEvent<DiseaseDiagnoserComponent, DiseaseDiagnosisDoAfterEvent>(OnDiagnosisDoAfter);
        SubscribeLocalEvent<VaccinatorComponent, AfterInteractUsingEvent>(OnVaccinatorInsert);
        SubscribeLocalEvent<VaccinatorComponent, VaccineProductionDoAfterEvent>(OnVaccineProductionDoAfter);
        SubscribeLocalEvent<PathogenVaccineComponent, AfterInteractEvent>(OnVaccineApply);
        SubscribeLocalEvent<PathogenSampleComponent, ExaminedEvent>(OnSampleExamined);
    }

    private void OnSwabInteract(Entity<DiseasePathogenSwabComponent> swab, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || args.Handled)
            return;

        if (swab.Comp.Used || HasComp<PathogenSampleComponent>(swab))
        {
            _popup.PopupEntity(Loc.GetString("sol-swab-already-used"), swab, args.User);
            return;
        }

        if (!HasComp<MobStateComponent>(args.Target))
            return;

        if (!_pathogen.IsVirologyEnabledAt(args.Target.Value))
        {
            // Still allow collecting an empty/negative sample for UX consistency on non-virology stations.
        }

        CollectSwabSample(swab, args.Target.Value, args.User);
        args.Handled = true;
    }

    public void CollectSwabSample(Entity<DiseasePathogenSwabComponent> swab, EntityUid patient, EntityUid user)
    {
        swab.Comp.Used = true;
        var sample = EnsureComp<PathogenSampleComponent>(swab);
        sample.Used = false;
        sample.IsBloodSample = false;

        if (TryComp<PathogenCarrierComponent>(patient, out var carrier) && carrier.Infections.Count > 0)
        {
            // Prefer a symptomatic infection; incubation may false-negative.
            ActivePathogenInfection? chosen = null;
            foreach (var infection in carrier.Infections)
            {
                chosen = infection;
                if (infection.Stage != PathogenStage.Incubation)
                    break;
            }

            if (chosen != null)
            {
                sample.PathogenId = chosen.PathogenId;
                sample.Dose = chosen.Dose;
                sample.DetectedStage = chosen.Stage;
                sample.ForceNegative = chosen.Stage == PathogenStage.Incubation;
            }
        }

        // Also sample surface contamination on the patient.
        if (sample.PathogenId == null && TryComp<SurfaceContaminationComponent>(patient, out var surface))
        {
            foreach (var entry in surface.Contaminants)
            {
                sample.PathogenId = entry.PathogenId;
                sample.Dose = entry.Load;
                break;
            }
        }

        Dirty(swab.Owner, sample);
        _popup.PopupEntity(Loc.GetString("sol-swab-collected", ("target", Identity.Entity(patient, EntityManager))), swab, user);
    }

    private void OnDiagnoserInsert(Entity<DiseaseDiagnoserComponent> machine, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!TryComp<PathogenSampleComponent>(args.Used, out var sample))
            return;

        if (machine.Comp.Processing)
        {
            _popup.PopupEntity(Loc.GetString("sol-disease-machine-busy"), machine, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            machine.Comp.AnalysisDelay,
            new DiseaseDiagnosisDoAfterEvent(),
            machine,
            target: args.Used,
            used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        machine.Comp.Processing = true;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("sol-diagnoser-started"), machine, args.User);
    }

    private void OnDiagnosisDoAfter(Entity<DiseaseDiagnoserComponent> machine, ref DiseaseDiagnosisDoAfterEvent args)
    {
        machine.Comp.Processing = false;

        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        if (!TryComp<PathogenSampleComponent>(args.Target.Value, out var sample))
            return;

        PrintDiagnosis(machine, args.Target.Value, sample, args.User);
        args.Handled = true;
    }

    public void PrintDiagnosis(
        Entity<DiseaseDiagnoserComponent> machine,
        EntityUid sampleEntity,
        PathogenSampleComponent sample,
        EntityUid user)
    {
        var report = Spawn(machine.Comp.ReportPrototype, Transform(machine).Coordinates);
        var paper = EnsureComp<PaperComponent>(report);

        string body;
        if (sample.ForceNegative || sample.PathogenId == null)
        {
            body = Loc.GetString("sol-diagnoser-report-negative");
        }
        else if (_pathogen.TryResolvePathogen(sample.PathogenId.Value, out var pathogen) && pathogen != null)
        {
            var stage = sample.DetectedStage?.ToString() ?? "Unknown";
            body = Loc.GetString("sol-diagnoser-report-positive",
                ("disease", pathogen.DisplayName),
                ("stage", stage),
                ("dose", sample.Dose.ToString("F1")),
                ("blood", sample.IsBloodSample));

            foreach (var tracker in EntityQuery<BioterrorCellTrackerComponent>())
                tracker.Diagnosed = true;
        }
        else
        {
            body = Loc.GetString("sol-diagnoser-report-inconclusive");
        }

        _paper.SetContent((report, paper), body);
        _meta.SetEntityName(report, Loc.GetString("sol-diagnoser-report-name"));
        _popup.PopupEntity(Loc.GetString("sol-diagnoser-complete"), machine, user);
    }

    private void OnVaccinatorInsert(Entity<VaccinatorComponent> machine, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!TryComp<PathogenSampleComponent>(args.Used, out var sample) || sample.PathogenId == null)
        {
            _popup.PopupEntity(Loc.GetString("sol-vaccinator-need-sample"), machine, args.User);
            return;
        }

        if (sample.ForceNegative)
        {
            _popup.PopupEntity(Loc.GetString("sol-vaccinator-bad-sample"), machine, args.User);
            return;
        }

        if (machine.Comp.Processing)
        {
            _popup.PopupEntity(Loc.GetString("sol-disease-machine-busy"), machine, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            machine.Comp.ProductionDelay,
            new VaccineProductionDoAfterEvent(),
            machine,
            target: args.Used,
            used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        machine.Comp.Processing = true;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("sol-vaccinator-started"), machine, args.User);
    }

    private void OnVaccineProductionDoAfter(Entity<VaccinatorComponent> machine, ref VaccineProductionDoAfterEvent args)
    {
        machine.Comp.Processing = false;

        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        if (!TryComp<PathogenSampleComponent>(args.Target.Value, out var sample) ||
            sample.PathogenId == null ||
            sample.ForceNegative)
            return;

        var vaccine = Spawn(machine.Comp.VaccinePrototype, Transform(machine).Coordinates);
        var vac = EnsureComp<PathogenVaccineComponent>(vaccine);
        vac.PathogenId = sample.PathogenId;
        if (_pathogen.TryResolvePathogen(sample.PathogenId.Value, out var pathogen) && pathogen != null)
        {
            vac.VaccineIdentity = string.IsNullOrEmpty(pathogen.VaccineIdentity) ? pathogen.Id : pathogen.VaccineIdentity;
            vac.Duration = pathogen.VaccineImmunityDuration;
            var ev = new BioterrorVaccineCreatedEvent(pathogen.Id, machine);
            RaiseLocalEvent(ref ev);
        }
        else
        {
            vac.VaccineIdentity = sample.PathogenId.Value;
        }

        vac.Strength = 0f;
        Dirty(vaccine, vac);
        QueueDel(args.Target.Value);
        _popup.PopupEntity(Loc.GetString("sol-vaccinator-produced"), machine, args.User);
        args.Handled = true;
    }

    private void OnVaccineApply(Entity<PathogenVaccineComponent> vaccine, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || args.Handled)
            return;

        if (!HasComp<MobStateComponent>(args.Target))
            return;

        if (!_pathogen.TryVaccinate(args.Target.Value, vaccine.Comp))
        {
            _popup.PopupEntity(Loc.GetString("sol-vaccine-failed"), vaccine, args.User);
            return;
        }

        args.Handled = true;
        Dirty(vaccine);
        _popup.PopupEntity(Loc.GetString("sol-vaccine-applied", ("target", Identity.Entity(args.Target.Value, EntityManager))), vaccine, args.User);
        QueueDel(vaccine);
    }

    private void OnSampleExamined(Entity<PathogenSampleComponent> sample, ref ExaminedEvent args)
    {
        if (sample.Comp.IsBloodSample)
            args.PushMarkup(Loc.GetString(sample.Comp.IsCentrifuged ? "sol-sample-blood-centrifuged" : "sol-sample-blood"));
        else
            args.PushMarkup(Loc.GetString("sol-sample-swab"));
    }
}
