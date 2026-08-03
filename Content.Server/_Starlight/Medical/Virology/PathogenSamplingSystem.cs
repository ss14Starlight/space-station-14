using System.Linq;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Labels.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Handles anonymous collection, vial preparation, multi-vial centrifuging, and
/// diagnoser report/culture output.
/// </summary>
public sealed partial class PathogenSamplingSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> CentrifugeCompatibleTag = "CentrifugeCompatible";
    private static readonly EntProtoId ReportPrototype = "DiagnosisReportPaper";
    private static readonly EntProtoId ViableCulturePrototype = "PathogenViableCulture";

    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PathogenContaminationSourceSystem _sources = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private PathogenTransmissionSystem _transmission = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenSwabComponent, AfterInteractEvent>(OnSwabInteract);
        SubscribeLocalEvent<PathogenSwabComponent, PathogenSwabDoAfterEvent>(OnSwabDoAfter);
        SubscribeLocalEvent<PathogenSpecimenComponent, AfterInteractEvent>(OnSpecimenInteract);
        SubscribeLocalEvent<PathogenSpecimenComponent, PathogenDiagnoseDoAfterEvent>(OnDiagnoseDoAfter);
        SubscribeLocalEvent<PathogenCentrifugeComponent, ComponentInit>(OnCentrifugeInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PathogenCentrifugeComponent>();
        while (query.MoveNext(out var uid, out var centrifuge))
        {
            if (!centrifuge.Processing ||
                _timing.CurTime < centrifuge.FinishAt ||
                !_power.IsPowered(uid))
            {
                continue;
            }

            FinishCentrifuge((uid, centrifuge));
        }
    }

    private void OnSwabInteract(Entity<PathogenSwabComponent> swab, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach)
            return;

        if (swab.Comp.Filled)
        {
            TryTransferToVial(swab, target, args.User);
            return;
        }

        if (!CanSample(target))
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-swab-no-sample"),
                swab,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            swab.Comp.SampleTime,
            new PathogenSwabDoAfterEvent(),
            swab,
            target,
            swab)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private bool CanSample(EntityUid target)
    {
        if (_sources.TryGetSampleStrain(target, out _))
            return true;

        return TryComp<PathogenInfectionComponent>(target, out var infections) &&
               infections.Infections.Count > 0;
    }

    private void OnSwabDoAfter(Entity<PathogenSwabComponent> swab, ref PathogenSwabDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || swab.Comp.Filled)
            return;

        if (_sources.TryGetSampleStrain(target, out var sourceStrain))
        {
            FillSwab(swab, sourceStrain.Id, null, sourceSample: true);
            _transmission.TryExpose(args.User, sourceStrain, 1f);
        }
        else if (TryComp<PathogenInfectionComponent>(target, out var infections) &&
                 infections.Infections.FirstOrDefault() is { } infection &&
                 _registry.TryGetStrain(infection.Pathogen, out _))
        {
            FillSwab(swab, infection.Pathogen, target, sourceSample: false);
        }
        else
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-swab-no-sample"),
                swab,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("pathogen-swab-collected"),
            swab,
            args.User);
        args.Handled = true;
    }

    private void FillSwab(
        Entity<PathogenSwabComponent> swab,
        int strain,
        EntityUid? host,
        bool sourceSample)
    {
        swab.Comp.Strain = strain;
        swab.Comp.Host = host;
        swab.Comp.SourceSample = sourceSample;
        _metaData.SetEntityName(swab, Loc.GetString("pathogen-swab-filled-name"));
    }

    private void TryTransferToVial(
        Entity<PathogenSwabComponent> swab,
        EntityUid target,
        EntityUid user)
    {
        if (MetaData(target).EntityPrototype?.ID != "ChemistryEmptyVialSmall" ||
            HasComp<PathogenSpecimenComponent>(target) ||
            !_solutions.TryGetSolution(target, "drink", out _, out var solution) ||
            solution.Volume > 0)
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-swab-requires-empty-vial"),
                swab,
                user,
                PopupType.SmallCaution);
            return;
        }

        var specimen = EnsureComp<PathogenSpecimenComponent>(target);
        specimen.Strain = swab.Comp.Strain;
        specimen.Host = swab.Comp.Host;
        specimen.SourceSample = swab.Comp.SourceSample;
        _tags.RemoveTag(target, CentrifugeCompatibleTag);
        EnsureComp<LabelComponent>(target);
        _metaData.SetEntityName(target, Loc.GetString("pathogen-specimen-vial-name"));
        QueueDel(swab);

        _popup.PopupEntity(
            Loc.GetString("pathogen-swab-transferred"),
            target,
            user);
    }

    private void OnCentrifugeInit(
        Entity<PathogenCentrifugeComponent> centrifuge,
        ref ComponentInit args)
    {
        centrifuge.Comp.Container = _containers.EnsureContainer<Container>(
            centrifuge,
            PathogenCentrifugeComponent.ContainerId);
    }

    private void OnSpecimenInteract(Entity<PathogenSpecimenComponent> specimen, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach)
            return;

        if (TryComp<PathogenCentrifugeComponent>(target, out var centrifuge))
        {
            TryInsertIntoCentrifuge(specimen, (target, centrifuge), args.User);
            args.Handled = true;
            return;
        }

        if (TryComp<PathogenDiagnoserComponent>(target, out var diagnoser))
        {
            TryStartDiagnosis(specimen, (target, diagnoser), args.User);
            args.Handled = true;
        }
    }

    private void TryInsertIntoCentrifuge(
        Entity<PathogenSpecimenComponent> specimen,
        Entity<PathogenCentrifugeComponent> centrifuge,
        EntityUid user)
    {
        if (specimen.Comp.Analysable)
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-centrifuge-already-processed"),
                centrifuge,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (!_power.IsPowered(centrifuge))
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-machine-no-power"),
                centrifuge,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (!_solutions.TryGetSolution(specimen.Owner, "drink", out _, out var solution) ||
            solution.GetTotalPrototypeQuantity("Water") <= 0)
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-centrifuge-needs-water"),
                specimen,
                user,
                PopupType.SmallCaution);
            return;
        }

        var container = centrifuge.Comp.Container ??
                        _containers.EnsureContainer<Container>(
                            centrifuge,
                            PathogenCentrifugeComponent.ContainerId);
        centrifuge.Comp.Container = container;
        if (container.Count >= centrifuge.Comp.Capacity)
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-centrifuge-full"),
                centrifuge,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (!_containers.Insert(specimen.Owner, container))
            return;

        if (!centrifuge.Comp.Processing)
        {
            centrifuge.Comp.Processing = true;
            centrifuge.Comp.FinishAt = _timing.CurTime + centrifuge.Comp.ProcessTime;
            _appearance.SetData(
                centrifuge,
                SolutionContainerMixerVisuals.Mixing,
                true);
        }

        _popup.PopupEntity(
            Loc.GetString(
                "pathogen-centrifuge-inserted",
                ("count", container.Count),
                ("capacity", centrifuge.Comp.Capacity)),
            centrifuge,
            user);
    }

    private void FinishCentrifuge(Entity<PathogenCentrifugeComponent> centrifuge)
    {
        centrifuge.Comp.Processing = false;
        _appearance.SetData(
            centrifuge,
            SolutionContainerMixerVisuals.Mixing,
            false);

        if (centrifuge.Comp.Container is not { } container)
            return;

        var completed = 0;
        foreach (var vial in container.ContainedEntities.ToArray())
        {
            if (TryComp<PathogenSpecimenComponent>(vial, out var specimen))
            {
                specimen.Analysable = true;
                _metaData.SetEntityName(vial, Loc.GetString("pathogen-culture-vial-name"));
                completed++;
            }

            _containers.Remove(
                vial,
                container,
                destination: Transform(centrifuge).Coordinates);
        }

        _popup.PopupEntity(
            Loc.GetString("pathogen-centrifuge-complete", ("count", completed)),
            centrifuge);
    }

    private void TryStartDiagnosis(
        Entity<PathogenSpecimenComponent> specimen,
        Entity<PathogenDiagnoserComponent> diagnoser,
        EntityUid user)
    {
        if (!specimen.Comp.Analysable)
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-diagnoser-not-ready"),
                specimen,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (!_power.IsPowered(diagnoser))
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-machine-no-power"),
                diagnoser,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (_registry.CanAnalyzeSample(
                specimen.Comp.Strain,
                specimen.Comp.Host,
                specimen.Comp.SourceSample) == PathogenAnalysisResult.DuplicateHost)
        {
            DuplicateHostPopup(diagnoser, user);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            diagnoser.Comp.AnalysisTime,
            new PathogenDiagnoseDoAfterEvent(),
            specimen,
            diagnoser,
            specimen)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDiagnoseDoAfter(
        Entity<PathogenSpecimenComponent> specimen,
        ref PathogenDiagnoseDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Handled ||
            args.Target is not { } target ||
            !HasComp<PathogenDiagnoserComponent>(target) ||
            !_power.IsPowered(target))
        {
            return;
        }

        var result = _registry.AnalyzeSample(
            specimen.Comp.Strain,
            specimen.Comp.Host,
            specimen.Comp.SourceSample);
        if (result == PathogenAnalysisResult.DuplicateHost)
        {
            DuplicateHostPopup(target, args.User);
            return;
        }

        if (result is PathogenAnalysisResult.Invalid or PathogenAnalysisResult.Accepted ||
            !_registry.TryGetStrain(specimen.Comp.Strain, out var strain))
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-diagnoser-invalid"),
                target,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        QueueDel(specimen);
        PrintReport(target, strain);

        // Any analysis of an already-identified strain regrows a culture. Identification
        // lives on the strain, so losing the culture must not mean redoing the diagnosis.
        if (result is PathogenAnalysisResult.Completed or PathogenAnalysisResult.AlreadyComplete)
            SpawnViableCulture(target, strain);

        _popup.PopupEntity(
            result is PathogenAnalysisResult.Completed or PathogenAnalysisResult.AlreadyComplete
                ? Loc.GetString("pathogen-diagnoser-complete")
                : Loc.GetString("pathogen-diagnoser-partial"),
            target,
            args.User);
        args.Handled = true;
    }

    private void DuplicateHostPopup(EntityUid target, EntityUid user)
    {
        _popup.PopupEntity(
            Loc.GetString("pathogen-diagnoser-duplicate-host"),
            target,
            user,
            PopupType.MediumCaution);
    }

    private void PrintReport(EntityUid diagnoser, Pathogen strain)
    {
        var report = Spawn(ReportPrototype, Transform(diagnoser).Coordinates);
        _paper.SetContent(report, BuildReport(strain));
    }

    private void SpawnViableCulture(EntityUid diagnoser, Pathogen strain)
    {
        var culture = Spawn(ViableCulturePrototype, Transform(diagnoser).Coordinates);
        Comp<PathogenViableCultureComponent>(culture).Strain = strain.Id;
        _metaData.SetEntityName(
            culture,
            Loc.GetString(
                "pathogen-viable-culture-designated-name",
                ("designation", strain.Designation)));
    }

    public string BuildReport(Pathogen strain)
    {
        var complete = strain.Identification == PathogenIdentificationStage.Complete;
        var insufficient = Loc.GetString("pathogen-diagnosis-insufficient");
        var symptoms = string.Join(
            ", ",
            strain.Symptoms.Select(symptom =>
                _prototypes.TryIndex(symptom, out var prototype)
                    ? Loc.GetString(prototype.Name)
                    : symptom.Id));
        var origin = strain.Beneficial || strain.Tier == PathogenTier.Virulent
            ? Loc.GetString("pathogen-origin-engineered")
            : Loc.GetString("pathogen-origin-natural");

        return Loc.GetString(
            "pathogen-diagnosis-report",
            ("designation", strain.Designation),
            ("classification", Loc.GetString(
                $"pathogen-classification-{strain.PathogenType.ToString().ToLowerInvariant()}")),
            ("symptoms", symptoms),
            ("incubation", complete ? FormatDuration(strain.Incubation) : insufficient),
            ("duration", complete ? FormatDuration(strain.Duration) : insufficient),
            ("transmissibility", complete
                ? strain.Transmissibility.ToString("P1")
                : insufficient),
            ("origin", complete ? origin : insufficient),
            ("conclusion", complete
                ? Loc.GetString("pathogen-diagnosis-complete")
                : Loc.GetString("pathogen-diagnosis-incomplete")));
    }

    private static string FormatDuration(TimeSpan time)
    {
        if (time == TimeSpan.Zero)
            return "INDEFINITE";

        if (time.TotalMinutes >= 1)
            return $"{Math.Floor(time.TotalMinutes):0}m {time.Seconds}s";

        return $"{time.TotalSeconds:0}s";
    }
}
