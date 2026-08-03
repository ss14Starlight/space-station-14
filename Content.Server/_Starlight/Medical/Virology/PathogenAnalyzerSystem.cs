using System.Linq;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Produces a direct, target-specific pathogen reading without advancing identification.
/// </summary>
public sealed partial class PathogenAnalyzerSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PathogenContaminationSourceSystem _sources = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PathogenAnalyzerComponent, PathogenAnalyzerDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<PathogenAnalyzerComponent> analyzer, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach || !CanScan(target))
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            analyzer.Comp.ScanTime,
            new PathogenAnalyzerDoAfterEvent(),
            analyzer,
            target,
            analyzer)
        {
            NeedHand = true,
            BreakOnMove = true,
            DistanceThreshold = 1.5f,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<PathogenAnalyzerComponent> analyzer, ref PathogenAnalyzerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanScan(target))
            return;

        _ui.SetUiState(analyzer.Owner, PathogenAnalyzerUiKey.Key, BuildState(target));
        _ui.TryOpenUi(analyzer.Owner, PathogenAnalyzerUiKey.Key, args.User);
        args.Handled = true;
    }

    public bool CanScan(EntityUid target)
    {
        if (HasComp<HumanoidAppearanceComponent>(target) && HasComp<MobStateComponent>(target))
            return true;

        if (HasComp<PathogenViableCultureComponent>(target) ||
            HasComp<PathogenSpecimenComponent>(target) ||
            HasComp<PathogenInjectorComponent>(target) ||
            HasComp<PathogenSporePatchComponent>(target))
        {
            return true;
        }

        return _sources.TryGetSourcePathogens(target, out _);
    }

    public PathogenAnalyzerUiState BuildState(EntityUid target)
    {
        var kind = GetTargetKind(target);
        var readings = new List<(int Strain, string Context)>();

        if (TryComp<PathogenInfectionComponent>(target, out var infections))
        {
            foreach (var infection in infections.Infections)
            {
                if (!_registry.TryGetStrain(infection.Pathogen, out var strain))
                    continue;

                readings.Add((
                    strain.Id,
                    Loc.GetString(
                        "pathogen-analyzer-context-patient",
                        ("stage", infection.Stage),
                        ("maxStage", strain.MaxStage))));
            }
        }
        else if (TryComp<PathogenViableCultureComponent>(target, out var culture))
        {
            readings.Add((culture.Strain, Loc.GetString("pathogen-analyzer-context-viable-culture")));
        }
        else if (TryComp<PathogenSpecimenComponent>(target, out var specimen))
        {
            var context = specimen.Analysable
                ? "pathogen-analyzer-context-analysable-culture"
                : "pathogen-analyzer-context-unprepared-culture";
            readings.Add((specimen.Strain, Loc.GetString(context)));
        }
        else if (TryComp<PathogenInjectorComponent>(target, out var injector) && !injector.Empty)
        {
            var mode = injector.Mode switch
            {
                PathogenInjectorMode.Treatment => "pathogen-analyzer-injector-treatment",
                PathogenInjectorMode.LiveVaccine => "pathogen-analyzer-injector-live",
                PathogenInjectorMode.BeneficialStrain => "pathogen-analyzer-injector-beneficial",
                _ => "pathogen-analyzer-injector-empty",
            };
            readings.Add((
                injector.Strain,
                Loc.GetString(
                    "pathogen-analyzer-context-injector",
                    ("mode", Loc.GetString(mode)),
                    ("doses", injector.Doses),
                    ("capacity", injector.MaxDoses))));
        }
        else if (TryComp<PathogenSporePatchComponent>(target, out var patch) && patch.Strain > 0)
        {
            readings.Add((patch.Strain, Loc.GetString("pathogen-analyzer-context-source")));
        }
        else if (_sources.TryGetSourcePathogens(target, out var sourceStrains))
        {
            foreach (var strain in sourceStrains)
                readings.Add((strain.Id, Loc.GetString("pathogen-analyzer-context-source")));
        }

        var entries = new List<PathogenAnalyzerEntry>();
        foreach (var (strainId, context) in readings.DistinctBy(reading => reading.Strain))
        {
            if (_registry.TryGetStrain(strainId, out var strain))
                entries.Add(BuildEntry(strain, context));
        }

        return new PathogenAnalyzerUiState(Name(target), kind, entries);
    }

    private PathogenAnalyzerTargetKind GetTargetKind(EntityUid target)
    {
        if (HasComp<HumanoidAppearanceComponent>(target) && HasComp<MobStateComponent>(target))
            return PathogenAnalyzerTargetKind.Patient;

        if (HasComp<PathogenInjectorComponent>(target))
            return PathogenAnalyzerTargetKind.Injector;

        if (HasComp<PathogenViableCultureComponent>(target) || HasComp<PathogenSpecimenComponent>(target))
            return PathogenAnalyzerTargetKind.Culture;

        return PathogenAnalyzerTargetKind.ContaminationSource;
    }

    private PathogenAnalyzerEntry BuildEntry(Pathogen strain, string context)
    {
        if (strain.Identification != PathogenIdentificationStage.Complete)
        {
            return new PathogenAnalyzerEntry(
                false,
                Loc.GetString("pathogen-analyzer-unidentified"),
                context,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        var symptoms = string.Join(", ", strain.Symptoms.Select(symptom =>
            _prototypes.TryIndex(symptom, out var prototype)
                ? Loc.GetString(prototype.Name)
                : symptom.Id));
        var origin = strain.Beneficial || strain.Tier == PathogenTier.Virulent
            ? Loc.GetString("pathogen-origin-engineered")
            : Loc.GetString("pathogen-origin-natural");

        return new PathogenAnalyzerEntry(
            true,
            strain.Designation,
            context,
            Loc.GetString($"pathogen-classification-{strain.PathogenType.ToString().ToLowerInvariant()}"),
            Loc.GetString($"pathogen-tier-{strain.Tier.ToString().ToLowerInvariant()}"),
            origin,
            symptoms,
            FormatDuration(strain.Incubation),
            FormatDuration(strain.Duration),
            strain.Transmissibility.ToString("P1"),
            strain.ProtectionBypass.ToString("P0"),
            strain.MaxPrevalence.ToString("P0"));
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
