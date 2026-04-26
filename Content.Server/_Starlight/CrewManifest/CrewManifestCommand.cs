using System.Linq;
using Content.Server.Administration;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Administration;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.CrewManifest;

[AdminCommand(AdminFlags.Fun)]
[ToolshedCommand]
public sealed class CrewManifestCommand : ToolshedCommand
{
    [Dependency] private readonly IPlayerManager _plr = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private StationRecordsSystem? _records;
    private JobSystem? _job;
    private MindSystem? _mind;
    private ContainerSystem? _container;
    private InventorySystem? _inventory;
    private static readonly string AssistantPrototypeId = "Assistant";

    [CommandImplementation("addto")]
    public EntityUid AddToManifest([PipedArgument] EntityUid uid, EntityUid station, bool useIdJob, bool addRole)
    {
        AddRecord(station, uid, useIdJob, addRole);
        return uid;
    }

    [CommandImplementation("addto")]
    public IEnumerable<EntityUid> AddToManifest([PipedArgument] IEnumerable<EntityUid> uids, EntityUid station, bool useIdJob, bool addRole) =>
        uids.Select(x => AddToManifest(x, station, useIdJob, addRole));

    [CommandImplementation("removefrom")]
    public EntityUid RemoveFromManifest([PipedArgument] EntityUid uid, EntityUid station)
    {
        RemoveRecord(station, uid);
        return uid;
    }

    [CommandImplementation("removefrom")]
    public IEnumerable<EntityUid> RemoveFromManifest([PipedArgument] IEnumerable<EntityUid> uids, EntityUid station) =>
        uids.Select(x => RemoveFromManifest(x, station));

    [CommandImplementation("addplayer")]
    public EntityUid AddPlayerToManifest([PipedArgument] EntityUid station, EntityUid uid, bool useIdJob, bool addRole)
    {
        AddRecord(station, uid, useIdJob, addRole);
        return station;
    }

    [CommandImplementation("addplayer")]
    public IEnumerable<EntityUid> AddPlayerToManifest([PipedArgument] IEnumerable<EntityUid> stations, EntityUid uid, bool useIdJob, bool addRole) =>
        stations.Select(x => AddPlayerToManifest(x, uid, useIdJob, addRole));

    [CommandImplementation("removeplayer")]
    public EntityUid RemovePlayerFromManifest([PipedArgument] EntityUid station, EntityUid uid)
    {
        RemoveRecord(station, uid);
        return station;
    }

    [CommandImplementation("removeplayer")]
    public IEnumerable<EntityUid> RemovePlayerFromManifest([PipedArgument] IEnumerable<EntityUid> stations, EntityUid uid) =>
        stations.Select(x => RemovePlayerFromManifest(x, uid));

    private ProtoId<JobPrototype> GetJobOrDefault(EntityUid player)
    {
        // Attempt to fetch job from current ID for convenience. Otherwise, this will forcefully set the player's job role to Assistant.
        _job ??= EntitySystemManager.GetEntitySystem<JobSystem>();
        _container ??= EntitySystemManager.GetEntitySystem<ContainerSystem>();
        _inventory ??= EntitySystemManager.GetEntitySystem<InventorySystem>();

        if (!_inventory.TryGetSlotEntity(player, "id", out var target)) return AssistantPrototypeId;
        if (TryComp<PdaComponent>(target, out var pda) && pda.ContainedId is { } id &&
            TryComp<IdCardComponent>(id, out var card))
        {
            if(card.JobPrototype is not null) return card.JobPrototype.Value; // yayyyy no shitty parsinggg
            // this next part can fail based off if the prototype sucks ass and id names are inconsistent. womp womp. i'm not editing every single id prototype.
            var iconId = card.JobIcon.Id;
            var parsed = iconId.Replace("Icon", "").Replace("Job", "");
            if(_proto.HasIndex<JobPrototype>(parsed)) return _proto.Index<JobPrototype>(parsed); // pray.
        }

        return AssistantPrototypeId;
    }

    private void AddRecord(EntityUid station, EntityUid player, bool useIdJob, bool addRole)
    {
        _records ??= EntitySystemManager.GetEntitySystem<StationRecordsSystem>();
        _job ??= EntitySystemManager.GetEntitySystem<JobSystem>();
        _mind ??= EntitySystemManager.GetEntitySystem<MindSystem>();
        _inventory ??= EntitySystemManager.GetEntitySystem<InventorySystem>();
        if (!_plr.TryGetSessionByEntity(player, out var session) || !_mind.TryGetMind(session.UserId, out var mind) ||
            !TryComp<StationRecordsComponent>(station, out var records)) return;
        _inventory.TryGetSlotEntity(player, "id", out var target);
        _job.MindTryGetJobId(mind, out var jobId);
        if (useIdJob)
        {
            jobId = GetJobOrDefault(player);
            if (addRole) _job.MindAddJob(mind.Value, jobId);
        }
        TryComp<HumanoidAppearanceComponent>(player, out var humanoid);
        TryComp<FingerprintComponent>(player, out var fingerprint);
        TryComp<DnaComponent>(player, out var dna);
        var name = MetaData(player).EntityName;
        var age = humanoid?.Age ?? 0;
        var gender = humanoid?.Gender ?? Gender.Epicene;
        var species = !string.IsNullOrEmpty(humanoid?.CustomSpecieName)
            ? $"{humanoid.CustomSpecieName} ({humanoid.Species})"
            : humanoid?.Species.Id ?? "Unknown";
        _records.CreateGeneralRecord(station, target, name, age, species, gender, jobId ?? "Unknown",
            fingerprint?.Fingerprint, dna?.DNA, null, records);
    }

    private void RemoveRecord(EntityUid station, EntityUid player)
    {
        _records ??= EntitySystemManager.GetEntitySystem<StationRecordsSystem>();
        if (!TryComp<StationRecordsComponent>(station, out var records)) return;
        if (_records.GetRecordByName(station, MetaData(player).EntityName, records) is not { } id) return;
        var key = new StationRecordKey(id, station);
        _records.RemoveRecord(key, records);
    }
}
