using System.Globalization;
using System.Linq;
using Content.Server.Administration;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Administration;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Console;
using Robust.Shared.Localization;
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

    private static readonly ProtoId<JobPrototype> VirologistJob = "Virologist";

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly string[] Subcommands =
    [
        "smoke",
        "setup",
        "kit",
        "generate",
        "strains",
        "status",
        "infect",
        "cure",
        "stage",
        "fast",
        "identify",
        "protection",
        "expose",
        "contamination",
        "sample",
        "sweep",
        "spore",
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

    private static readonly EntProtoId[] TestKit =
    [
        "HandheldVirologyMonitor",
        "PathogenAnalyzer",
        "PathogenSwab",
        "PathogenInjector",
        "ChemistryEmptyVialSmall",
        "PathogenDecontaminator",
        "DiseaseDiagnoser",
        "MachineCentrifuge",
        "ClothingMaskGas",
        "ClothingMaskSterile",
        "ClothingMaskBreathMedical",
        "ClothingHandsGlovesLatex",
        "ClothingHandsGlovesNitrile",
        "ClothingOuterBioGeneral",
        "ClothingHeadHatHoodBioGeneral",
        "ClothingOuterHardsuitEngineering",
        "PathogenViableCulture",
        "BiosealRollerBed",
        "ViroculumSeeds",
    ];

    /// <summary>
    /// Everything virology adds that a round has to be able to produce. The smoke pass
    /// only checks these resolve; <see cref="TestKit"/> is the subset worth holding.
    /// </summary>
    private static readonly EntProtoId[] SmokeEntities =
    [
        "HandheldVirologyMonitor",
        "PathogenAnalyzer",
        "PathogenSwab",
        "PathogenViableCulture",
        "ComputerVirologyDetector",
        "PathogenDecontaminator",
        "PathogenSporePatch",
        "PathogenInjector",
        "BiosealRollerBed",
        "BiosealRollerBedSpawnFolded",
        "ViroculumSeeds",
        "FoodViroculumCap",
    ];

    private static readonly string[] SmokeRecipes =
    [
        "HandheldVirologyMonitor",
        "PathogenSwab",
        "PathogenAnalyzer",
        "PathogenInjector",
    ];

    public string Command => "virotest";

    public string Description => "Creates and controls a live-round virology test environment.";

    public string Help =>
        "virotest smoke [self|netEntity]\n" +
        "virotest setup [self|netEntity]\n" +
        "virotest kit [self|netEntity]\n" +
        "virotest generate <archetype>\n" +
        "virotest strains\n" +
        "virotest status [self|netEntity]\n" +
        "virotest infect <self|netEntity> <strainId>\n" +
        "virotest cure <self|netEntity> <strainId|all>\n" +
        "virotest stage <self|netEntity> <strainId> <stage>\n" +
        "virotest fast <self|netEntity> <strainId|all> [seconds|off]\n" +
        "virotest identify <self|netEntity> <strainId|all>\n" +
        "virotest protection <self|netEntity> <strainId>\n" +
        "virotest expose <self|netEntity> <strainId> [baseChance]\n" +
        "virotest contamination [virus bacteria fungus]\n" +
        "virotest sample\n" +
        "virotest sweep\n" +
        "virotest spore <self|netEntity> <fungalStrainId>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "smoke":
                Smoke(shell, args);
                break;
            case "setup":
                Setup(shell, args);
                break;
            case "kit":
                SpawnKit(shell, args);
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
            case "protection":
                Protection(shell, args);
                break;
            case "expose":
                Expose(shell, args);
                break;
            case "contamination":
                Contamination(shell, args);
                break;
            case "sample":
                Sample(shell, args);
                break;
            case "sweep":
                Sweep(shell, args);
                break;
            case "spore":
                SpawnSpore(shell, args);
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

        return CompletionResult.Empty;
    }

    /// <summary>
    /// One-shot existence pass over everything virology adds. Answers "is the content
    /// reachable" before anyone spends time on "is the content correct", and catches the
    /// failure mode that no other test does: a prototype that loads fine but whose locale
    /// key was never written, so it shows up in game as a raw fluent id.
    /// </summary>
    private void Smoke(IConsoleShell shell, string[] args)
    {
        if (args.Length > 2 ||
            !TryResolveOptionalTarget(shell, args, 1, out var target))
        {
            shell.WriteLine("Usage: virotest smoke [self|netEntity]");
            return;
        }

        var checks = 0;
        var failures = 0;

        void Check(bool passed, string label)
        {
            checks++;
            if (passed)
                return;

            failures++;
            shell.WriteError($"  FAIL  {label}");
        }

        shell.WriteLine("[entities]");
        foreach (var prototype in SmokeEntities)
            Check(_prototypes.HasIndex<EntityPrototype>(prototype), $"entity '{prototype}'");

        shell.WriteLine("[job and recipes]");
        Check(_prototypes.HasIndex<JobPrototype>(VirologistJob), $"job '{VirologistJob}'");
        foreach (var recipe in SmokeRecipes)
            Check(_prototypes.HasIndex<LatheRecipePrototype>(recipe), $"lathe recipe '{recipe}'");

        shell.WriteLine("[archetypes]");
        foreach (var archetype in _prototypes.EnumeratePrototypes<PathogenArchetypePrototype>()
                     .OrderBy(archetype => archetype.ID))
        {
            Check(_loc.HasString(archetype.Name), $"archetype '{archetype.ID}' name '{archetype.Name}'");

            if (archetype.Description is { } description)
                Check(_loc.HasString(description), $"archetype '{archetype.ID}' description '{description}'");

            foreach (var symptom in archetype.CoreSymptoms
                         .Concat(archetype.StageOneSymptomPool)
                         .Concat(archetype.SymptomPool))
            {
                Check(
                    _prototypes.HasIndex(symptom),
                    $"archetype '{archetype.ID}' references missing symptom '{symptom}'");
            }
        }

        shell.WriteLine("[symptoms]");
        var referenced = _prototypes.EnumeratePrototypes<PathogenArchetypePrototype>()
            .SelectMany(archetype => archetype.CoreSymptoms
                .Concat(archetype.StageOneSymptomPool)
                .Concat(archetype.SymptomPool))
            .Select(symptom => symptom.Id)
            .ToHashSet();

        foreach (var symptom in _prototypes.EnumeratePrototypes<PathogenSymptomPrototype>()
                     .OrderBy(symptom => symptom.ID))
        {
            Check(_loc.HasString(symptom.Name), $"symptom '{symptom.ID}' name '{symptom.Name}'");

            // Unreachable symptoms are finished work that silently never fires.
            Check(referenced.Contains(symptom.ID), $"symptom '{symptom.ID}' is not in any archetype pool");
        }

        shell.WriteLine($"[result] {checks - failures}/{checks} checks passed.");

        if (failures > 0)
            shell.WriteError($"{failures} check(s) failed - fix these before testing behaviour.");

        EnsureTestStrains(shell);
        SpawnKit(shell, target);
    }

    private void Setup(IConsoleShell shell, string[] args)
    {
        if (args.Length > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        EnsureTestStrains(shell);

        if (TryResolveOptionalTarget(shell, args, 1, out var target))
            SpawnKit(shell, target);
        else
            shell.WriteLine("Test strains are ready. Run 'virotest kit <target>' to spawn equipment.");
    }

    private void SpawnKit(IConsoleShell shell, string[] args)
    {
        if (args.Length > 2 ||
            !TryResolveOptionalTarget(shell, args, 1, out var target))
        {
            shell.WriteLine("Usage: virotest kit [self|netEntity]");
            return;
        }

        SpawnKit(shell, target);
    }

    private void SpawnKit(IConsoleShell shell, EntityUid target)
    {
        if (!_entities.TryGetComponent<TransformComponent>(target, out var transform))
        {
            shell.WriteError("The target has no transform.");
            return;
        }

        var spawned = 0;
        foreach (var prototype in TestKit)
        {
            if (!_prototypes.HasIndex<EntityPrototype>(prototype))
            {
                shell.WriteError($"Missing test-kit prototype '{prototype}'.");
                continue;
            }

            _entities.SpawnEntity(prototype, transform.Coordinates);
            spawned++;
        }

        shell.WriteLine($"Spawned {spawned} virology tools and PPE items at {Pretty(target)}.");
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
            $"PPE bypass={strain.ProtectionBypass:P1}; prevalence={strain.MaxPrevalence:P1}");
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
        var resistance = _entities.System<PathogenResistanceSystem>();
        foreach (var infection in infections.Infections.OrderBy(infection => infection.Pathogen))
        {
            if (!registry.TryGetStrain(infection.Pathogen, out var strain))
            {
                shell.WriteLine($"  #{infection.Pathogen}: missing strain");
                continue;
            }

            var coefficient = resistance.GetResistance(target, strain);
            var nextStage = Math.Max(0d, (infection.NextStage - _timing.CurTime).TotalSeconds);
            var remaining = infection.EndTime == TimeSpan.Zero
                ? "never"
                : $"{Math.Max(0d, (infection.EndTime - _timing.CurTime).TotalSeconds):0.0}s";

            shell.WriteLine(
                $"  #{strain.Id} {strain.Designation}: stage={infection.Stage}/{strain.MaxStage}, " +
                $"next={nextStage:0.0}s, clears={remaining}, identification={strain.Identification}, " +
                $"effectiveProtection={1f - coefficient:P1}, exposureMultiplier={coefficient:P1}, " +
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
        var infected = pathogen.TryInfect(target, strain.Id, bypassImmunity: true);
        if (infected)
            pathogen.TrySetSymptomInterval(target, strain.Id, DefaultTestSymptomInterval);

        shell.WriteLine(infected
            ? $"Infected {Pretty(target)} with #{strain.Id} {strain.Designation}; " +
              $"test symptom interval={DefaultTestSymptomInterval.TotalSeconds:0.0}s."
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
                if (pathogen.Cure(target, strainId))
                    cured++;
            }

            shell.WriteLine($"Cured {cured} infection(s) from {Pretty(target)}.");
            return;
        }

        if (!TryParseStrain(shell, args[2], out var strain))
            return;

        shell.WriteLine(pathogen.Cure(target, strain.Id)
            ? $"Cured #{strain.Id} from {Pretty(target)}."
            : $"{Pretty(target)} does not carry #{strain.Id}.");
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
    }

    private void Protection(IConsoleShell shell, string[] args)
    {
        if (args.Length != 3 ||
            !TryResolveTarget(shell, args[1], out var target) ||
            !TryParseStrain(shell, args[2], out var strain))
        {
            shell.WriteLine("Usage: virotest protection <self|netEntity> <strainId>");
            return;
        }

        var coefficient = _entities.System<PathogenResistanceSystem>().GetResistance(target, strain);
        shell.WriteLine(
            $"{Pretty(target)} versus #{strain.Id} {strain.Designation}: " +
            $"effectiveProtection={1f - coefficient:P1}, exposureMultiplier={coefficient:P1}, " +
            $"strainPpeBypass={strain.ProtectionBypass:P1}.");
    }

    private void Expose(IConsoleShell shell, string[] args)
    {
        if (args.Length is < 3 or > 4 ||
            !TryResolveTarget(shell, args[1], out var target) ||
            !TryParseStrain(shell, args[2], out var strain))
        {
            shell.WriteLine("Usage: virotest expose <self|netEntity> <strainId> [baseChance]");
            return;
        }

        var baseChance = 1f;
        if (args.Length == 4 && !TryParseFloat(args[3], out baseChance))
        {
            shell.WriteError($"'{args[3]}' is not a valid chance.");
            return;
        }

        baseChance = Math.Clamp(baseChance, 0f, 1f);
        var coefficient = _entities.System<PathogenResistanceSystem>().GetResistance(target, strain);
        var effectiveChance = baseChance * coefficient;
        var infected = _entities.System<PathogenTransmissionSystem>().TryExpose(target, strain, baseChance);

        shell.WriteLine(
            $"Natural exposure of {Pretty(target)} to #{strain.Id}: base={baseChance:P1}, " +
            $"afterPPE={effectiveChance:P1}, result={(infected ? "infected" : "not infected")}.");
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
            $"physicalSources={sources.ActiveSourceCount}");
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
    }

    private void Sweep(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Usage: virotest sweep");
            return;
        }

        _entities.System<PathogenSpreadSystem>().SweepNow();
        shell.WriteLine("Ran one normal virus proximity sweep.");
    }

    private void SpawnSpore(IConsoleShell shell, string[] args)
    {
        if (args.Length != 3 ||
            !TryResolveTarget(shell, args[1], out var target) ||
            !TryParseStrain(shell, args[2], out var strain))
        {
            shell.WriteLine("Usage: virotest spore <self|netEntity> <fungalStrainId>");
            return;
        }

        if (strain.PathogenType != PathogenType.Fungus)
        {
            shell.WriteError($"#{strain.Id} is {strain.PathogenType}, not Fungus.");
            return;
        }

        if (!_entities.TryGetComponent<TransformComponent>(target, out var transform))
        {
            shell.WriteError("The target has no transform.");
            return;
        }

        var patch = _entities.SpawnEntity("PathogenSporePatch", transform.Coordinates);
        _entities.GetComponent<PathogenSporePatchComponent>(patch).Strain = strain.Id;
        shell.WriteLine($"Spawned fungal patch {Pretty(patch)} pinned to #{strain.Id}.");
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
}
