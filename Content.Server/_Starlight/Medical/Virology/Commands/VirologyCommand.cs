using System.Globalization;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Medical.Virology.Commands;

/// <summary>
/// The permanent admin-facing controls for running disease as an event: build a tuned
/// strain, infect or cure targeted hosts, or stop everything. Distinct from
/// <see cref="VirologyTestCommand"/>, which is developer scaffolding for verifying
/// mechanics rather than running a shift.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class VirologyCommand : IConsoleCommand
{
    private const int DefaultReleaseHosts = 2;

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>
    /// How many hosts one roster line names before it summarises the rest. The roster is
    /// meant to be readable at a glance during a shift, not a complete census.
    /// </summary>
    private const int RosterHostLimit = 10;

    private static readonly string[] Subcommands =
    [
        "custom",
        "infect",
        "cure",
        "cureall",
        "infected",
        "help",
    ];

    public string Command => "virology";

    public string Description => "Runs disease as an admin event: build a strain, infect hosts, or cure the station.";

    public string Help =>
        "virology custom <archetype> [hosts=n] [spread=x] [stageDelay=x] [cap=x] [symptoms=n]\n" +
        "  Creates a runtime strain from an archetype and optionally infects random living crew.\n" +
        $"  hosts is the number of random hosts to infect; default {DefaultReleaseHosts}, use 0 to only create the strain.\n" +
        "  spread and stageDelay are multipliers; 1 is the authored value, 0 disables that value.\n" +
        "  cap is the maximum share of living crew that can carry it, from 0 to 1.\n" +
        "  symptoms is the exact number of extra random symptoms to add.\n" +
        "  examples: virology custom SpaceCold hosts=2\n" +
        "            virology custom GreyLung hosts=0 spread=0.5 stageDelay=0.25 cap=0.30 symptoms=3\n" +
        "virology infect <self|netEntity> <strainId>\n" +
        "virology cure <self|netEntity> <strainId|all>\n" +
        "virology cureall\n" +
        "virology infected [strainId]\n" +
        "  Lists every strain someone is currently carrying, with its share of living crew\n" +
        "  against its own cap. Pass a strain id for the full host list and their stages.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "custom":
                Custom(shell, args);
                break;
            case "infect":
                Infect(shell, args);
                break;
            case "cure":
                Cure(shell, args);
                break;
            case "cureall":
                CureAll(shell, args);
                break;
            case "infected":
                Infected(shell, args);
                break;
            case "help":
                shell.WriteLine(Help);
                break;
            default:
                shell.WriteError($"Unknown virology subcommand '{args[0]}'.");
                shell.WriteLine(Help);
                break;
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(Subcommands, "<subcommand>");

        if (args.Length == 2 &&
            args[0].Equals("custom", StringComparison.OrdinalIgnoreCase))
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<PathogenArchetypePrototype>(),
                "<pathogenArchetype>");
        }

        return CompletionResult.Empty;
    }

    private void Custom(IConsoleShell shell, string[] args)
    {
        if (args.Length < 2 ||
            !TryResolveArchetype(shell, args[1], out var archetype))
        {
            shell.WriteLine(Help);
            return;
        }

        var options = new PathogenGenerationOptions();
        var hostCount = DefaultReleaseHosts;

        foreach (var argument in args.Skip(2))
        {
            var parts = argument.Split('=', 2);
            if (parts.Length != 2)
            {
                shell.WriteError($"'{argument}' is not a key=value option.");
                return;
            }

            var key = parts[0].ToLowerInvariant();
            if (key == "hosts")
            {
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out hostCount) ||
                    hostCount < 0)
                {
                    shell.WriteError($"'{parts[1]}' is not a valid host count.");
                    return;
                }

                continue;
            }

            if (key == "symptoms")
            {
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var extra) ||
                    extra < 0)
                {
                    shell.WriteError($"'{parts[1]}' is not a valid symptom count.");
                    return;
                }

                options.StageTwoSymptomCount = extra;
                continue;
            }

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                value < 0f)
            {
                shell.WriteError($"'{parts[1]}' is not a valid value for '{key}'.");
                return;
            }

            switch (key)
            {
                case "spread":
                    options.TransmissibilityMultiplier = value;
                    break;
                case "stagedelay":
                case "stages":
                    options.StageDelayMultiplier = value;
                    break;
                case "cap":
                    options.MaxPrevalenceCap = Math.Clamp(value, 0f, 1f);
                    break;
                default:
                    shell.WriteError($"Unknown option '{key}'.");
                    shell.WriteLine(Help);
                    return;
            }
        }

        var strain = _entities.System<PathogenRegistrySystem>().Generate(archetype, options);
        Release(shell, strain, hostCount, "custom strain");
    }

    private void Infect(IConsoleShell shell, string[] args)
    {
        if (args.Length != 3 ||
            !TryResolveTarget(shell, args[1], out var target) ||
            !TryParseStrain(shell, args[2], out var strain))
        {
            shell.WriteLine("Usage: virology infect <self|netEntity> <strainId>");
            return;
        }

        var infected = _entities.System<PathogenSystem>().TryInfect(
            target,
            strain.Id,
            cause: "admin infect");

        shell.WriteLine(infected
            ? $"Infected {Pretty(target)} with #{strain.Id} {strain.Designation}."
            : $"Could not infect {Pretty(target)} with #{strain.Id}; it may already carry the strain, be immune to it, or be an invalid host.");

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} ran virology infect on {_entities.ToPrettyString(target):target} with {strain.Designation:strain} (infected={infected})");
    }

    private void Cure(IConsoleShell shell, string[] args)
    {
        if (args.Length != 3 ||
            !TryResolveTarget(shell, args[1], out var target))
        {
            shell.WriteLine("Usage: virology cure <self|netEntity> <strainId|all>");
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
                if (pathogen.Cure(target, strainId, grantImmunity: true, cause: "admin cure"))
                    cured++;
            }

            shell.WriteLine($"Cured {cured} infection(s) from {Pretty(target)}.");
            _adminLog.Add(LogType.Virology, LogImpact.Medium,
                $"{Actor(shell)} ran virology cure all on {_entities.ToPrettyString(target):target} ({cured} cleared)");
            return;
        }

        if (!TryParseStrain(shell, args[2], out var strain))
            return;

        var removed = pathogen.Cure(target, strain.Id, grantImmunity: true, cause: "admin cure");
        shell.WriteLine(removed
            ? $"Cured #{strain.Id} from {Pretty(target)}."
            : $"{Pretty(target)} does not carry #{strain.Id}.");

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} ran virology cure on {_entities.ToPrettyString(target):target} for {strain.Designation:strain} (cured={removed})");
    }

    private void Release(IConsoleShell shell, Pathogen strain, int hostCount, string cause)
    {
        if (hostCount == 0)
        {
            WriteRelease(shell, strain, infected: 0);
            _adminLog.Add(LogType.Virology, LogImpact.Medium,
                $"{Actor(shell)} created {cause} {strain.Designation:strain} ({strain.Tier}/{strain.PathogenType}) without infecting any hosts");
            return;
        }

        var hosts = _entities.System<PathogenHostSelectionSystem>().GetEligibleAutomaticHosts();
        if (hosts.Count == 0)
        {
            shell.WriteError("No eligible hosts are available - nobody is alive and playable.");
            return;
        }

        _random.Shuffle(hosts);

        var pathogen = _entities.System<PathogenSystem>();
        var infected = 0;
        foreach (var host in hosts)
        {
            if (infected >= hostCount)
                break;

            if (pathogen.TryInfect(host, strain.Id, bypassImmunity: true, cause: $"admin {cause}"))
                infected++;
        }

        WriteRelease(shell, strain, infected);

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} released {cause} {strain.Designation:strain} ({strain.Tier}/{strain.PathogenType}) on {infected} host(s)");
    }

    private static void WriteRelease(IConsoleShell shell, Pathogen strain, int infected)
    {
        shell.WriteLine(
            $"Released #{strain.Id} {strain.Designation} [{strain.Archetype}] " +
            $"{strain.Tier}/{strain.PathogenType} on {infected} host(s); " +
            $"transmission={strain.Transmissibility:P1}; prevalence cap={strain.MaxPrevalence:P1}; " +
            $"stages={strain.MaxStage}.");
    }

    /// <summary>
    /// The panic button. An outbreak that has got out of hand needs one action that stops
    /// it, not a cure applied person by person while it keeps spreading behind you.
    /// </summary>
    private void CureAll(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Usage: virology cureall");
            return;
        }

        // Collected first: curing clears the component, and that must not happen while the
        // query that produced it is still being enumerated.
        var carriers = new List<EntityUid>();
        var query = _entities.EntityQueryEnumerator<PathogenInfectionComponent>();
        while (query.MoveNext(out var uid, out _))
            carriers.Add(uid);

        var pathogen = _entities.System<PathogenSystem>();
        var cured = 0;
        foreach (var carrier in carriers)
        {
            if (!_entities.TryGetComponent<PathogenInfectionComponent>(carrier, out var infections))
                continue;

            foreach (var infection in infections.Infections.ToArray())
            {
                if (pathogen.Cure(carrier, infection.Pathogen, grantImmunity: true, cause: "admin cureall"))
                    cured++;
            }
        }

        shell.WriteLine($"Cured {cured} infection(s) across {carriers.Count} carrier(s).");

        _adminLog.Add(LogType.Virology, LogImpact.Medium,
            $"{Actor(shell)} cured every active infection on the station ({cured} across {carriers.Count} carriers)");
    }

    /// <summary>
    /// Who is carrying what, right now. Dead carriers are counted separately because a
    /// corpse keeps its infection but does not occupy prevalence budget, so folding it into
    /// the share would report an outbreak as larger than the cap actually treats it.
    /// </summary>
    private void Infected(IConsoleShell shell, string[] args)
    {
        if (args.Length > 2)
        {
            shell.WriteLine("Usage: virology infected [strainId]");
            return;
        }

        Pathogen? requested = null;
        if (args.Length == 2)
        {
            if (!TryParseStrain(shell, args[1], out var parsed))
                return;

            requested = parsed;
        }

        var living = new Dictionary<int, List<EntityUid>>();
        var dead = new Dictionary<int, List<EntityUid>>();
        var mobStates = _entities.GetEntityQuery<MobStateComponent>();

        var query = _entities.EntityQueryEnumerator<PathogenInfectionComponent>();
        while (query.MoveNext(out var uid, out var infections))
        {
            var isDead = mobStates.TryGetComponent(uid, out var mobState) &&
                mobState.CurrentState == MobState.Dead;

            foreach (var infection in infections.Infections)
            {
                if (requested is { } only && infection.Pathogen != only.Id)
                    continue;

                var bucket = isDead ? dead : living;
                if (!bucket.TryGetValue(infection.Pathogen, out var hosts))
                    bucket[infection.Pathogen] = hosts = new List<EntityUid>();

                hosts.Add(uid);
            }
        }

        var registry = _entities.System<PathogenRegistrySystem>();
        var livingCrew = _entities.System<PathogenHostSelectionSystem>().CountLivingCrew();

        shell.WriteLine($"Active infections - living crew={livingCrew}");

        var strainIds = new HashSet<int>(living.Keys);
        strainIds.UnionWith(dead.Keys);

        if (strainIds.Count == 0)
        {
            shell.WriteLine("  no active infections.");
            return;
        }

        var rows = strainIds
            .Select(id => (Id: id, Strain: registry.TryGetStrain(id, out var strain) ? strain : null))
            .Where(row => row.Strain is not null)
            .OrderByDescending(row => row.Strain!.Tier)
            .ThenByDescending(row => living.GetValueOrDefault(row.Id)?.Count ?? 0)
            .ThenBy(row => row.Id);

        foreach (var (id, strain) in rows)
        {
            var alive = living.GetValueOrDefault(id) ?? new List<EntityUid>();
            var corpses = dead.GetValueOrDefault(id) ?? new List<EntityUid>();

            var share = livingCrew > 0 ? alive.Count / (float) livingCrew : 0f;
            var deadNote = corpses.Count > 0 ? $" +{corpses.Count} dead" : string.Empty;

            shell.WriteLine(
                $"#{id} {strain!.Designation} [{strain.Archetype}] {strain.Tier}/{strain.PathogenType} " +
                $"{alive.Count} host(s){deadNote} {share:P1}/{strain.MaxPrevalence:P0}");

            if (requested is null)
            {
                shell.WriteLine($"  {FormatHosts(alive)}");
                continue;
            }

            foreach (var host in alive)
                shell.WriteLine($"  {Pretty(host)} {FormatStage(host, id, strain)}");

            foreach (var host in corpses)
                shell.WriteLine($"  {Pretty(host)} {FormatStage(host, id, strain)} (dead)");
        }
    }

    private string FormatHosts(List<EntityUid> hosts)
    {
        if (hosts.Count == 0)
            return "no living hosts";

        var shown = hosts
            .Take(RosterHostLimit)
            .Select(host => _entities.GetNetEntity(host).ToString());

        var line = string.Join(", ", shown);
        var hidden = hosts.Count - RosterHostLimit;

        return hidden > 0 ? $"{line}, +{hidden} more" : line;
    }

    private string FormatStage(EntityUid host, int strainId, Pathogen strain)
    {
        if (!_entities.TryGetComponent<PathogenInfectionComponent>(host, out var infections))
            return "stage ?";

        foreach (var infection in infections.Infections)
        {
            if (infection.Pathogen == strainId)
                return $"stage {infection.Stage}/{strain.MaxStage}";
        }

        return "stage ?";
    }

    private bool TryResolveArchetype(
        IConsoleShell shell,
        string value,
        out PathogenArchetypePrototype archetype)
    {
        if (!_prototypes.TryIndex(new ProtoId<PathogenArchetypePrototype>(value), out var found))
        {
            shell.WriteError($"Unknown pathogen archetype '{value}'.");
            archetype = default!;
            return false;
        }

        archetype = found;
        return true;
    }

    private bool TryParseStrain(IConsoleShell shell, string value, out Pathogen strain)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var strainId) ||
            !_entities.System<PathogenRegistrySystem>().TryGetStrain(strainId, out var found))
        {
            shell.WriteError($"Unknown runtime strain id '{value}'. Create one with 'virology custom <archetype> hosts=0'.");
            strain = default!;
            return false;
        }

        strain = found;
        return true;
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

    private string Pretty(EntityUid uid)
        => $"{_entities.ToPrettyString(uid)} [net {_entities.GetNetEntity(uid)}]";

    private static string Actor(IConsoleShell shell)
        => shell.Player?.Name ?? "An administrator";
}
