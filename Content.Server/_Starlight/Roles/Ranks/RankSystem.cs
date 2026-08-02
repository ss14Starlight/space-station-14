using Content.Server._NullLink.PlayerData;
using Content.Server.Access.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._NullLink;
using Content.Shared._Starlight.Roles.Ranks;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Roles.Ranks;

public sealed partial class RankSystem : EntitySystem
{
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly NullLinkPlayerManager _linkPlayerManager = default!;
    [Dependency] private readonly StationRecordsSystem _record = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!_idCard.TryFindIdCard(ev.Mob, out var idCard))
            return;

        if (ev.JobId == null)
            return;

        if (!_prototype.TryIndex(ev.JobId, out JobPrototype? job))
            return;

        if (!TryGetHighestEligibleRank(ev.Player.UserId, job, null, out var highestRank))
            return;

        if (highestRank == null)
            return;

        if (!_prototype.TryIndex(highestRank.Icon, out var jobIcon))
            return;

        var jobName = Loc.GetString(highestRank.Name);

        _idCard.TryChangeJobTitle(idCard, jobName, idCard.Comp, ev.Player.AttachedEntity);
        _idCard.TryChangeJobIcon(idCard, jobIcon, idCard.Comp, ev.Player.AttachedEntity);

        if (!TryComp<StationRecordsComponent>(ev.Station, out var stationRecords))
            return;

        TryComp<FingerprintComponent>(ev.Player.AttachedEntity, out var fingerprintComponent);
        TryComp<DnaComponent>(ev.Player.AttachedEntity, out var dnaComponent);

        _record.CreateGeneralRecord(
            ev.Station,
            idCard,
            ev.Profile.Name,
            ev.Profile.Age,
            ev.Profile.CustomSpecieName,
            ev.Profile.Gender,
            ev.JobId,
            fingerprintComponent?.Fingerprint,
            dnaComponent?.DNA,
            ev.Profile,
            stationRecords);

        if (!TryComp<StationRecordKeyStorageComponent>(idCard.Owner, out var keyStorage)
            || keyStorage.Key is not { } key
            || !_record.TryGetRecord<GeneralStationRecord>(key, out var record))
            return;

        record.JobTitle = jobName;
        record.JobIcon = idCard.Comp.JobIcon;

        _record.Synchronize(key);
    }

    public bool TryGetHighestEligibleRank(Guid userId, JobPrototype? job, string? jobId, out RankPrototype? highestRank)
    {
        highestRank = null;

        //if (!_linkPlayerManager.TryGetPlayerData(userId, out PlayerData? playerData))
        //    return false;

        if (jobId != null && _prototype.TryIndex(jobId, out JobPrototype? jobPrototype))
            job = jobPrototype;

        if (job == null)
            return false;

        if (job.Ranks == null)
            return false;

        foreach (var rankId in job.Ranks)
        {
            if (!_prototype.TryIndex(rankId, out RankPrototype? rank))
                continue;

            if (!_prototype.TryIndex(rank.Requirement, out RoleRequirementPrototype? roleRequirement))
                continue;

            //if (!playerData.Roles.Overlaps(roleRequirement.Roles))
            //    continue;

            highestRank ??= rank;

            if (highestRank.Priority < rank.Priority)
                highestRank = rank;
        }

        if (highestRank != null)
            return true;

        return false;
    }
}
