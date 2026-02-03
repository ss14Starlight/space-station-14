using System.Linq;
using Content.Server._Starlight.HumanoidCharacterProfileMemory;
using Content.Server.Administration;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Server.StationRecords.Systems;
using Content.Shared.Administration;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Robust.Server.Player;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.CrewManifest;

[AdminCommand(AdminFlags.Fun)]
[ToolshedCommand]
public sealed class CrewManifestCommand : ToolshedCommand
{
    [Dependency] private readonly IPlayerManager _plr = default!;
    [Dependency] private readonly ILogManager _log = default!;
    private StationRecordsSystem? _records;
    private JobSystem? _job;
    private MindSystem? _mind;
    private HumanoidCharacterProfileMemorySystem? _profile;

    [CommandImplementation("addto")]
    public EntityUid AddToManifest([PipedArgument] EntityUid uid, EntityUid station)
    {
        _records ??= EntitySystemManager.GetEntitySystem<StationRecordsSystem>();
        _job ??= EntitySystemManager.GetEntitySystem<JobSystem>();
        _mind ??= EntitySystemManager.GetEntitySystem<MindSystem>();
        _profile ??= EntitySystemManager.GetEntitySystem<HumanoidCharacterProfileMemorySystem>();
        if (!_plr.TryGetSessionByEntity(uid, out var session) || !_mind.TryGetMind(session.UserId, out var mind) ||
            !_job.MindTryGetJobId(mind, out var jobId) ||
            !_profile.TryGetActiveProfile(session.UserId, out var profile) ||
            !TryComp<StationRecordsComponent>(station, out var records)) return uid;
        _records.AddPlayer(station, uid, profile, jobId, records);
        return uid;
    }

    [CommandImplementation("addto")]
    public IEnumerable<EntityUid> AddToManifest([PipedArgument] IEnumerable<EntityUid> uids, EntityUid station) =>
        uids.Select(x => AddToManifest(x, station));

    [CommandImplementation("removefrom")]
    public EntityUid RemoveFromManifest([PipedArgument] EntityUid uid, EntityUid station)
    {
        _records ??= EntitySystemManager.GetEntitySystem<StationRecordsSystem>();
        _profile ??= EntitySystemManager.GetEntitySystem<HumanoidCharacterProfileMemorySystem>();
        if (!_plr.TryGetSessionByEntity(uid, out var session) ||
            !_profile.TryGetActiveProfile(session.UserId, out var profile) ||
            !TryComp<StationRecordsComponent>(station, out var records)) return uid;
        _records.RemovePlayer(station, profile, records);
        return uid;
    }

    [CommandImplementation("removefrom")]
    public IEnumerable<EntityUid> RemoveFromManifest([PipedArgument] IEnumerable<EntityUid> uids, EntityUid station) =>
        uids.Select(x => RemoveFromManifest(x, station));
    
    [CommandImplementation("addplayer")]
    public EntityUid AddPlayerToManifest([PipedArgument] EntityUid station, EntityUid uid)
    {
        _records ??= EntitySystemManager.GetEntitySystem<StationRecordsSystem>();
        _job ??= EntitySystemManager.GetEntitySystem<JobSystem>();
        _mind ??= EntitySystemManager.GetEntitySystem<MindSystem>();
        _profile ??= EntitySystemManager.GetEntitySystem<HumanoidCharacterProfileMemorySystem>();
        if (!_plr.TryGetSessionByEntity(uid, out var session) || !_mind.TryGetMind(session.UserId, out var mind) ||
            !_job.MindTryGetJobId(mind, out var jobId) ||
            !_profile.TryGetActiveProfile(session.UserId, out var profile) ||
            !TryComp<StationRecordsComponent>(station, out var records)) return station;
        _records.AddPlayer(station, uid, profile, jobId, records);
        return station;
    }

    [CommandImplementation("addplayer")]
    public IEnumerable<EntityUid> AddPlayerToManifest([PipedArgument] IEnumerable<EntityUid> stations, EntityUid uid) =>
        stations.Select(x => AddPlayerToManifest(x, uid));

    [CommandImplementation("removeplayer")]
    public EntityUid RemovePlayerFromManifest([PipedArgument] EntityUid station, EntityUid uid)
    {
        _records ??= EntitySystemManager.GetEntitySystem<StationRecordsSystem>();
        _profile ??= EntitySystemManager.GetEntitySystem<HumanoidCharacterProfileMemorySystem>();
        if (!_plr.TryGetSessionByEntity(uid, out var session) ||
            !_profile.TryGetActiveProfile(session.UserId, out var profile) ||
            !TryComp<StationRecordsComponent>(station, out var records)) return station;
        _records.RemovePlayer(station, profile, records);
        return station;
    }

    [CommandImplementation("removeplayer")]
    public IEnumerable<EntityUid> RemovePlayerFromManifest([PipedArgument] IEnumerable<EntityUid> stations, EntityUid uid) =>
        stations.Select(x => RemovePlayerFromManifest(x, uid));
}