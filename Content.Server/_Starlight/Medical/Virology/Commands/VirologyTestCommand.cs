using System.Globalization;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology.Commands;

/// <summary>
/// Deterministic admin controls for exercising passive virology mechanics in a live test round.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class VirologyTestCommand : IConsoleCommand
{
    private static readonly TimeSpan DefaultTestSymptomInterval = TimeSpan.FromSeconds(3);

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly string[] Subcommands =
    [
        "setup",
        "generate",
        "strains",
        "status",
        "infect",
        "cure",
        "stage",
        "fast",
        "identify",
        "symptoms",
        "symptom",
        "contamination",
        "sample",
        "help",
    ];

    private static readonly ProtoId<PathogenArchetypePrototype>[] TestArchetypes =
    [
        "SpaceCold",
        "ThroatRot",
        "SporeBloom",
        "StationFlu",
        "GreyLung",
        "Mycosis",
    ];

    public string Command => "virotest";

    public string Description => "Controls runtime pathogen state for live virology testing.";

    public string Help =>
        "virotest setup\n" +
        "virotest generate <archetype>\n" +
        "virotest strains\n" +
        "virotest status [self|netEntity]\n" +
        "virotest infect <self|netEntity> <strainId>\n" +
        "virotest cure <self|netEntity> <strainId|all>\n" +
        "virotest stage <self|netEntity> <strainId> <stage>\n" +
        "virotest fast <self|netEntity> <strainId|all> [seconds|off]\n" +
        "virotest identify <self|netEntity> <strainId|all>\n" +
        "virotest symptoms\n" +
        "virotest symptom <self|netEntity> <symptomId>\n" +
        "virotest contamination [virus bacteria fungus]\n" +
        "virotest sample";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "setup":
                Setup(shell, args);
                break;
            case "generate":
                Generate(shell, args);
                break;
            case "strains":
                ListStrains(shell);
                break;
            case "status":
                Status(shell, args);
                break;
            case "infect":
                Infect(shell, args);
                break;
            case "cure":
                Cure(shell, args);
                break;
            case "stage":
                SetStage(shell, args);
                break;
            case "fast":
                FastSymptoms(shell, args);
                break;
            case "identify":
                Identify(shell, args);
                break;
            case "symptoms":
                ListSymptoms(shell, args);
                break;
            case "symptom":
                ForceSymptom(shell, args);
                break;
            case "contamination":
                Contamination(shell, args);
                break;
            case "sample":
                Sample(shell, args);
                break;
            case "help":
                shell.WriteLine(Help);
                break;
            default:
                shell.WriteError($"Unknown virotest subcommand '{args[0]}'.");
                shell.WriteLine(Help);
                break;
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(Subcommands, "<subcommand>");

        if (args.Length == 2 && args[0].Equals("generate", StringComparison.OrdinalIgnoreCase))
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<PathogenArchetypePrototype>(),
                "<pathogenArchetype>");
        }

        if (args.Length == 3 && args[0].Equals("symptom", StringComparison.OrdinalIgnoreCase))
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<PathogenSymptomPrototype>(),
                "<pathogenSymptom>");
        }

        return CompletionResult.Empty;
    }

    private void Setup(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Usage: virotest setup");
            return;
        }

        EnsureTestStrains(shell);
        shell.WriteLine("Test strains are ready.");
    }

    private void Generate(IConsoleShell shell, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine("Usage: virotest generate <archetype>");
            return;
        }

        var registry = _entities.System<PathogenRegistrySystem>();
        var strain = registry.Generate(new ProtoId<PathogenArchetypePrototype>(args[1]));
        if (strain is null)
        {
            shell.WriteError($"Unknown pathogen archetype '{args[1]}'.");
            return;
        }

        WriteStrain(shell, strain);

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} generated runtime strain {strain.Designation:strain} from archetype {args[1]}");
    }

    private void EnsureTestStrains(IConsoleShell shell)
    {
        var registry = _entities.System<PathogenRegistrySystem>();
        foreach (var archetype in TestArchetypes)
        {
            var strain = registry.Strains.Values.FirstOrDefault(existing => existing.Archetype == archetype) ??
                registry.Generate(archetype);

            if (strain is null)
            {
                shell.WriteError($"Could not generate test strain from '{archetype}'.");
                continue;
            }

            WriteStrain(shell, strain);
        }
    }

    private void ListStrains(IConsoleShell shell)
    {
        var strains = _entities.System<PathogenRegistrySystem>().Strains.Values
            .OrderBy(strain => strain.Id)
            .ToList();

        if (strains.Count == 0)
        {
            shell.WriteLine("No runtime pathogen strains exist.");
            return;
        }

        foreach (var strain in strains)
            WriteStrain(shell, strain);
    }

    private static void WriteStrain(IConsoleShell shell, Pathogen strain)
    {
        shell.WriteLine(
            $"#{strain.Id} {strain.Designation} [{strain.Archetype}] " +
            $"{strain.Tier}/{strain.PathogenType}; stages={strain.MaxStage}; " +
            $"transmission={strain.Transmissibility:P1}; range={strain.SpreadRange:0.00}; " +
            $"prevalence={strain.MaxPrevalence:P1}");
        shell.WriteLine($"  symptoms: {string.Join(", ", strain.Symptoms)}");
    }

    private void Status(IConsoleShell shell, string[] args)
    {
        if (args.Length > 2 ||
            !TryResolveOptionalTarget(shell, args, 1, out var target))
        {
            shell.WriteLine("Usage: virotest status [self|netEntity]");
            return;
        }

        var hostSelection = _entities.System<PathogenHostSelectionSystem>();
        shell.WriteLine(
            $"{Pretty(target)}: canHost={hostSelection.CanHost(target)}, " +
            $"automaticHost={hostSelection.IsEligibleAutomaticHost(target)}");

        if (!_entities.TryGetComponent<PathogenInfectionComponent>(target, out var infections) ||
            infections.Infections.Count == 0)
        {
            shell.WriteLine("  infections: none");
            return;
        }

        var registry = _entities.System<PathogenRegistrySystem>();
        foreach (var infection in infections.Infections.OrderBy(infection => infection.Pathogen))
        {
            if (!registry.TryGetStrain(infection.Pathogen, out var strain))
            {
                shell.WriteLine($"  #{infection.Pathogen}: missing strain");
                continue;
            }

            var nextStage = infection.Stage >= strain.MaxStage
                ? "max"
                : $"{Math.Max(0d, (infection.NextStage - _timing.CurTime).TotalSeconds):0.0}s";
            var remaining = infection.EndTime == TimeSpan.Zero
                ? "never"
                : $"{Math.Max(0d, (infection.EndTime - _timing.CurTime).TotalSeconds):0.0}s";

            shell.WriteLine(
                $"  #{strain.Id} {strain.Designation}: stage={infection.Stage}/{strain.MaxStage}, " +
                $"next={nextStage}, clears={remaining}, identification={strain.Identification}, " +
                $"symptomInterval={(infection.SymptomIntervalOverride is { } interval ? $"{interval.TotalSeconds:0.0}s" : "normal")}");
        }
    }

    private void Infect(IConsoleShell shell, string[] args)
    {
        if (args.Length != 3 ||
            !TryResolveTarget(shell, args[1], out var target) ||
            !TryParseStrain(shell, args[2], out var strain))
        {
            shell.WriteLine("Usage: virotest infect <self|netEntity> <strainId>");
            return;
        }

        var pathogen = _entities.System<PathogenSystem>();
        var infected = pathogen.TryInfect(target, strain.Id, bypassImmunity: true, cause: "virotest infect");
        if (infected)
            pathogen.TrySetSymptomInterval(target, strain.Id, DefaultTestSymptomInterval);

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} ran virotest infect on {_entities.ToPrettyString(target):target} with {strain.Designation:strain} (infected={infected})");

        shell.WriteLine(infected
            ? $"Force-infected {Pretty(target)} with #{strain.Id} {strain.Designation}; " +
                $"bypassed immunity; test symptom interval={DefaultTestSymptomInterval.TotalSeconds:0.0}s."
            : $"Could not infect {Pretty(target)} with #{strain.Id}; it may already carry the strain or be an invalid host.");
    }

    private void Cure(IConsoleShell shell, string[] args)
    {
        if (args.Length != 3 ||
            !TryResolveTarget(shell, args[1], out var target))
        {
            shell.WriteLine("Usage: virotest cure <self|netEntity> <strainId|all>");
            return;
        }

        var pathogen = _entities.System<PathogenSystem>();
        if (args[2].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (!_entities.TryGetComponent<PathogenInfectionComponent>(target, out var infections))
            {
                shell.WriteLine($"{Pretty(target)} has no infections.");
                return;
            }

            var cured = 0;
            foreach (var strainId in infections.Infections.Select(infection => infection.Pathogen).ToArray())
            {
                if (pathogen.Cure(target, strainId, cause: "virotest cure"))
                    cured++;
            }

            shell.WriteLine($"Cured {cured} infection(s) from {Pretty(target)}.");
            _adminLog.Add(LogType.Virology, LogImpact.Medium,
                $"{Actor(shell)} ran virotest cure all on {_entities.ToPrettyString(target):target} ({cured} cleared)");
            return;
        }

        if (!TryParseStrain(shell, args[2], out var strain))
            return;

        var removed = pathogen.Cure(target, strain.Id, cause: "virotest cure");
        shell.WriteLine(removed
            ? $"Cured #{strain.Id} from {Pretty(target)}."
            : $"{Pretty(target)} does not carry #{strain.Id}.");

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} ran virotest cure on {_entities.ToPrettyString(target):target} for {strain.Designation:strain} (cured={removed})");
    }

    private void SetStage(IConsoleShell shell, string[] args)
    {
        if (args.Length != 4 ||
            !TryResolveTarget(shell, args[1], out var target) ||
            !TryParseStrain(shell, args[2], out var strain) ||
            !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stage))
        {
            shell.WriteLine("Usage: virotest stage <self|netEntity> <strainId> <stage>");
            return;
        }

        var changed = _entities.System<PathogenSystem>().TrySetStage(target, strain.Id, stage, out var actual);
        shell.WriteLine(changed
            ? $"Set #{strain.Id} on {Pretty(target)} to stage {actual}/{strain.MaxStage}."
            : $"{Pretty(target)} does not carry #{strain.Id}.");

        if (changed)
        {
            _adminLog.Add(LogType.Virology, LogImpact.Medium,
                $"{Actor(shell)} set {strain.Designation:strain} on {_entities.ToPrettyString(target):target} to stage {actual}");
        }
    }

    private void FastSymptoms(IConsoleShell shell, string[] args)
    {
        if (args.Length is < 3 or > 4 ||
            !TryResolveTarget(shell, args[1], out var target))
        {
            shell.WriteLine("Usage: virotest fast <self|netEntity> <strainId|all> [seconds|off]");
            return;
        }

        TimeSpan? interval = DefaultTestSymptomInterval;
        if (args.Length == 4)
        {
            if (args[3].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                interval = null;
            }
            else if (!TryParseFloat(args[3], out var seconds) || seconds < 0.5f)
            {
                shell.WriteError("Symptom interval must be at least 0.5 seconds, or 'off'.");
                return;
            }
            else
            {
                interval = TimeSpan.FromSeconds(seconds);
            }
        }

        if (!_entities.TryGetComponent<PathogenInfectionComponent>(target, out var infections))
        {
            shell.WriteLine($"{Pretty(target)} has no infections.");
            return;
        }

        var all = args[2].Equals("all", StringComparison.OrdinalIgnoreCase);
        Pathogen? requested = null;
        if (!all)
        {
            if (!TryParseStrain(shell, args[2], out var parsed))
                return;

            requested = parsed;
        }

        var pathogen = _entities.System<PathogenSystem>();
        var changed = 0;
        foreach (var infection in infections.Infections)
        {
            if (!all && infection.Pathogen != requested!.Id)
                continue;

            if (pathogen.TrySetSymptomInterval(target, infection.Pathogen, interval))
                changed++;
        }

        var cadence = interval is { } value
            ? $"{value.TotalSeconds:0.0}s"
            : "normal";
        shell.WriteLine($"Set {changed} infection symptom interval(s) on {Pretty(target)} to {cadence}.");

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} set the symptom interval on {_entities.ToPrettyString(target):target} to {cadence} for {changed} infection(s)");
    }

    private void Identify(IConsoleShell shell, string[] args)
    {
        if (args.Length != 3 ||
            !TryResolveTarget(shell, args[1], out var target) ||
            !_entities.TryGetComponent<PathogenInfectionComponent>(target, out var infections))
        {
            shell.WriteLine("Usage: virotest identify <self|netEntity> <strainId|all>");
            return;
        }

        var identifyAll = args[2].Equals("all", StringComparison.OrdinalIgnoreCase);
        Pathogen? requested = null;
        if (!identifyAll)
        {
            if (!TryParseStrain(shell, args[2], out var parsed))
                return;

            requested = parsed;
        }

        var registry = _entities.System<PathogenRegistrySystem>();
        var changed = 0;
        foreach (var infection in infections.Infections)
        {
            if (!identifyAll && infection.Pathogen != requested!.Id)
                continue;

            if (!registry.TryGetStrain(infection.Pathogen, out var strain))
                continue;

            if (strain.Identification != PathogenIdentificationStage.Complete)
                changed++;
            registry.IdentifyFully(infection.Pathogen);
        }

        shell.WriteLine($"Fully identified {changed} strain(s) carried by {Pretty(target)}.");

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} fully identified {changed} strain(s) carried by {_entities.ToPrettyString(target):target}");
    }

    private void ListSymptoms(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Usage: virotest symptoms");
            return;
        }

        foreach (var symptom in _prototypes.EnumeratePrototypes<PathogenSymptomPrototype>()
                    .OrderBy(symptom => symptom.ID))
        {
            shell.WriteLine(
                $"{symptom.ID}: name={symptom.Name}; minStage={symptom.MinStage}; " +
                $"interval={symptom.Interval.TotalSeconds:0.0}s; effects={symptom.Effects.Count}");
        }
    }

    private void ForceSymptom(IConsoleShell shell, string[] args)
    {
        if (args.Length != 3 ||
            !TryResolveTarget(shell, args[1], out var target) ||
            !TryResolveSymptom(shell, args[2], out var symptomId, out var symptom))
        {
            shell.WriteLine("Usage: virotest symptom <self|netEntity> <symptomId>");
            return;
        }

        if (!_entities.System<PathogenSystem>().TryExpressSymptom(
                target,
                symptomId,
                out _))
        {
            shell.WriteError($"Could not express pathogen symptom '{symptom.ID}' on {Pretty(target)}.");
            return;
        }

        shell.WriteLine($"Applied {symptom.ID} to {Pretty(target)}.");

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} ran virotest symptom on {_entities.ToPrettyString(target):target} with {symptom.ID:symptom}");
    }

    private void Contamination(IConsoleShell shell, string[] args)
    {
        var contamination = _entities.System<PathogenContaminationSystem>();
        var sources = _entities.System<PathogenContaminationSourceSystem>();

        if (args.Length == 1)
        {
            WriteContamination(shell, contamination, sources);
            return;
        }

        if (args.Length != 4 ||
            !TryParseFloat(args[1], out var virus) ||
            !TryParseFloat(args[2], out var bacteria) ||
            !TryParseFloat(args[3], out var fungus))
        {
            shell.WriteLine("Usage: virotest contamination [virus bacteria fungus]");
            return;
        }

        contamination.SetContamination(new Dictionary<PathogenType, float>
        {
            [PathogenType.Virus] = Math.Max(0f, virus),
            [PathogenType.Bacteria] = Math.Max(0f, bacteria),
            [PathogenType.Fungus] = Math.Max(0f, fungus),
        });
        WriteContamination(shell, contamination, sources);

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} set station contamination to virus={virus:0.00}, bacteria={bacteria:0.00}, fungus={fungus:0.00}");
    }

    private static void WriteContamination(
        IConsoleShell shell,
        PathogenContaminationSystem contamination,
        PathogenContaminationSourceSystem sources)
    {
        shell.WriteLine(
            $"contamination={contamination.Contamination:0.00}; " +
            $"virus={contamination.GetContamination(PathogenType.Virus):0.00}; " +
            $"bacteria={contamination.GetContamination(PathogenType.Bacteria):0.00}; " +
            $"fungus={contamination.GetContamination(PathogenType.Fungus):0.00}; " +
            $"physicalSources={sources.ActiveSourceCount}; " +
            $"baselineSources={sources.BaselineSourceCount}; " +
            $"ignoredBaselineSources={sources.IgnoredBaselineSourceCount}; " +
            $"baselineEstablished={sources.HasBaseline}");
        shell.WriteLine($"  active sources: {FormatSourceReport(sources.SourceReport)}");
        shell.WriteLine($"  ignored baseline: {FormatSourceReport(sources.IgnoredBaselineReport)}");
    }

    private static string FormatSourceReport(IReadOnlyList<PathogenContaminationSourceReport> report)
    {
        if (report.Count == 0)
            return "none";

        return string.Join(
            "; ",
            report.Select(entry => $"{entry.Kind}: count={entry.Count}, value={entry.Contamination:0.00}"));
    }

    private void Sample(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Usage: virotest sample");
            return;
        }

        var sources = _entities.System<PathogenContaminationSourceSystem>();
        if (!sources.SampleNow())
        {
            shell.WriteError("Physical sources can only be sampled during a live round.");
            return;
        }

        WriteContamination(shell, _entities.System<PathogenContaminationSystem>(), sources);

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} forced a contamination sampling pass");
    }

    private bool TryParseStrain(IConsoleShell shell, string value, out Pathogen strain)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var strainId) ||
            !_entities.System<PathogenRegistrySystem>().TryGetStrain(strainId, out var found))
        {
            shell.WriteError($"Unknown runtime strain id '{value}'. Run 'virotest strains'.");
            strain = default!;
            return false;
        }

        strain = found;
        return true;
    }

    private bool TryResolveSymptom(
        IConsoleShell shell,
        string value,
        out ProtoId<PathogenSymptomPrototype> symptomId,
        out PathogenSymptomPrototype symptom)
    {
        symptomId = new ProtoId<PathogenSymptomPrototype>(value);
        if (_prototypes.TryIndex(symptomId, out var found))
        {
            symptom = found;
            return true;
        }

        shell.WriteError($"Unknown pathogen symptom '{value}'. Run 'virotest symptoms'.");
        symptom = default!;
        return false;
    }

    private bool TryResolveOptionalTarget(
        IConsoleShell shell,
        string[] args,
        int index,
        out EntityUid target)
    {
        if (args.Length > index)
            return TryResolveTarget(shell, args[index], out target);

        if (shell.Player?.AttachedEntity is { } attached)
        {
            target = attached;
            return true;
        }

        target = default;
        return false;
    }

    private bool TryResolveTarget(IConsoleShell shell, string value, out EntityUid target)
    {
        if (value.Equals("self", StringComparison.OrdinalIgnoreCase))
        {
            if (shell.Player?.AttachedEntity is { } attached)
            {
                target = attached;
                return true;
            }

            shell.WriteError("'self' requires a player-attached entity.");
            target = default;
            return false;
        }

        if (NetEntity.TryParse(value, out var netEntity) &&
            _entities.TryGetEntity(netEntity, out var entity) &&
            entity is { } resolved)
        {
            target = resolved;
            return true;
        }

        shell.WriteError($"'{value}' is not a valid network entity.");
        target = default;
        return false;
    }

    private static bool TryParseFloat(string value, out float result)
        => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private string Pretty(EntityUid uid)
        => $"{_entities.ToPrettyString(uid)} [net {_entities.GetNetEntity(uid)}]";

    private static string Actor(IConsoleShell shell)
        => shell.Player?.Name ?? "An administrator";
}
