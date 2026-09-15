using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Robust.Server.Containers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Access.Systems;
using Content.Shared.Access;
using Content.Shared._Starlight.Access;
using Robust.Shared.Utility;
using Content.Shared.GameTicking;
using Content.Shared.StationRecords;
using Content.Server.StationRecords.Systems;

namespace Content.Server.AlertLevel;

public sealed partial class AlertLevelSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    #region Starlight
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private SharedAccessSystem _accessSystem = default!;
    [Dependency] private IdCardSystem _cardSystem = default!;
    [Dependency] private StationRecordsSystem _stationRecord = default!;
    #endregion

    // Until stations are a prototype, this is how it's going to have to be.
    public const string DefaultAlertLevelSet = "stationAlerts";

    public override void Initialize()
    {
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialize);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload); //Starlight-edit: Start
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(UpdateTempIdAccessOnPlayerSpawn); //Starlight-edit: End
    }

    public override void Update(float time)
    {
        var query = EntityQueryEnumerator<AlertLevelComponent>();

        while (query.MoveNext(out var station, out var alert))
        {
            if (alert.CurrentDelay <= 0)
            {
                if (alert.ActiveDelay)
                {
                    RaiseLocalEvent(new AlertLevelDelayFinishedEvent());
                    alert.ActiveDelay = false;
                }
                continue;
            }

            alert.CurrentDelay -= time;
        }
    }

    private void OnStationInitialize(StationInitializedEvent args)
    {
        if (!TryComp<AlertLevelComponent>(args.Station, out var alertLevelComponent))
            return;

        if (!_prototypeManager.TryIndex(alertLevelComponent.AlertLevelPrototype, out AlertLevelPrototype? alerts))
        {
            return;
        }

        alertLevelComponent.AlertLevels = alerts;

        var defaultLevel = alertLevelComponent.AlertLevels.DefaultLevel;
        if (string.IsNullOrEmpty(defaultLevel))
        {
            defaultLevel = alertLevelComponent.AlertLevels.Levels.Keys.First();
        }

        SetLevel(args.Station, defaultLevel, false, false, true);
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        if (args.ByType.TryGetValue(typeof(AlertLevelPrototype), out var alertPrototypes)
            && alertPrototypes.Modified.TryGetValue(DefaultAlertLevelSet, out var alertObject)
            && alertObject is AlertLevelPrototype alerts)
        {

            var query = EntityQueryEnumerator<AlertLevelComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                comp.AlertLevels = alerts;

                if (!comp.AlertLevels.Levels.ContainsKey(comp.CurrentLevel))
                {
                    var defaultLevel = comp.AlertLevels.DefaultLevel;
                    if (string.IsNullOrEmpty(defaultLevel))
                    {
                        defaultLevel = comp.AlertLevels.Levels.Keys.First();
                    }

                    SetLevel(uid, defaultLevel, true, true, true);
                }
            }
        }

        // Starlight-edit: Start
        if (args.ByType.ContainsKey(typeof(AlertAccessPolicyPrototype)))
        {
            var query = EntityQueryEnumerator<AlertLevelComponent>();
            while (query.MoveNext(out var station, out var alertComp))
            {
                ApplyTemporaryAlertLevelAccessToStation(station, alertComp.CurrentLevel);
            }
        }
        // Starlight-edit: End

        RaiseLocalEvent(new AlertLevelPrototypeReloadedEvent());
    }

    public string GetLevel(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return string.Empty;
        }

        return alert.CurrentLevel;
    }

    public float GetAlertLevelDelay(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return float.NaN;
        }

        return alert.CurrentDelay;
    }

    /// <summary>
    /// Get the default alert level for a station entity.
    /// Returns an empty string if the station has no alert levels defined.
    /// </summary>
    /// <param name="station">The station entity.</param>
    public string GetDefaultLevel(Entity<AlertLevelComponent?> station)
    {
        if (!Resolve(station.Owner, ref station.Comp) || station.Comp.AlertLevels == null)
        {
            return string.Empty;
        }
        return station.Comp.AlertLevels.DefaultLevel;
    }

    /// <summary>
    /// Set the alert level based on the station's entity ID.
    /// </summary>
    /// <param name="station">Station entity UID.</param>
    /// <param name="level">Level to change the station's alert level to.</param>
    /// <param name="playSound">Play the alert level's sound.</param>
    /// <param name="announce">Say the alert level's announcement.</param>
    /// <param name="force">Force the alert change. This applies if the alert level is not selectable or not.</param>
    /// <param name="locked">Will it be possible to change level by crew.</param>
    public void SetLevel(EntityUid station, string level, bool playSound, bool announce, bool force = false,
        bool locked = false, MetaDataComponent? dataComponent = null, AlertLevelComponent? component = null, string? actor = null) // Starlight: +actor
    {
        if (!Resolve(station, ref component, ref dataComponent)
            || component.AlertLevels == null
            || !component.AlertLevels.Levels.TryGetValue(level, out var detail)
            || component.CurrentLevel == level)
        {
            return;
        }
        if (!force)
        {
            if (!detail.Selectable
                || component.CurrentDelay > 0
                || component.IsLevelLocked)
            {
                return;
            }

            component.CurrentDelay = _cfg.GetCVar(CCVars.GameAlertLevelChangeDelay);
            component.ActiveDelay = true;
        }

        var oldLevel = component.CurrentLevel; //Starlight
        component.CurrentLevel = level;
        component.IsLevelLocked = locked;

        var stationName = dataComponent.EntityName;

        var name = level.ToLower();

        if (Loc.TryGetString($"alert-level-{level}", out var locName))
        {
            name = locName.ToLower();
        }

        // Announcement text. Is passed into announcementFull.
        var announcement = detail.Announcement;

        if (Loc.TryGetString(detail.Announcement, out var locAnnouncement))
        {
            announcement = locAnnouncement;
        }

        // The full announcement to be spat out into chat.
        // Starlight BEGIN
        var announcementFull = actor != null
            ? Loc.GetString("alert-level-announcement-sender", ("name", name), ("announcement", announcement), ("sender", actor))
            : Loc.GetString("alert-level-announcement", ("name", name), ("announcement", announcement));
        // Starlight END

        var playDefault = false;
        if (playSound)
        {
            if (detail.Sound != null)
            {
                var filter = _stationSystem.GetInOwningStation(station);
                _audio.PlayGlobal(detail.Sound, filter, true, detail.Sound.Params);
            }
            else
            {
                playDefault = true;
            }
        }

        if (announce)
        {
            _chatSystem.DispatchStationAnnouncement(station, announcementFull, playDefaultSound: playDefault,
                colorOverride: detail.Color, sender: stationName);
        }

        ApplyTemporaryAlertLevelAccessToStation(station, level); // Starlight-edit

        RaiseLocalEvent(new AlertLevelChangedEvent(station, level, oldLevel)); // Starlight-edit: Add Old Level
    }

    #region Starlight

    /// <summary>
    /// Checks each ID card for those that are in PDA's on the given station when
    /// its alert level is set, and then checks each one
    /// to see if it adding/removing temporary acces based on the Station alert.
    /// </summary>
    /// <param name="station">The station whose alert level is changing</param>
    /// <param name="level">The new alert level that the station is changing to</param>
    public void ApplyTemporaryAlertLevelAccessToStation(EntityUid station, string level)
    {
        var query = AllEntityQuery<IdCardComponent>();

        while (query.MoveNext(out var idCardUid, out var idCardComp))
        {
            var parent = Transform(idCardUid).ParentUid;
            if (parent == EntityUid.Invalid)
                continue;

            if (!TryComp<PdaComponent>(parent, out _))
                continue;

            if (!_container.TryGetContainer(parent, "PDA-id", out _))
                continue;

            if (_stationSystem.GetOwningStation(parent) != station)
                continue;

            var jobPrototype = ResolveJobPrototype(idCardUid, idCardComp);
            SetTemporaryAlertAccessLevel(idCardUid, level, jobPrototype);
        }
    }

    /// <summary>
    /// Checks the ID against the policy to see if the character needs
    /// to recieve any temporary access, or have any temporary access removed.
    /// </summary>
    /// <param name="idCardUid">The uid of the affected Id card</param>
    /// <param name="level">The new alert level that the station is changing to</param>
    /// <param name="jobPrototype">The proto id of the characters job</param>
    public void SetTemporaryAlertAccessLevel(EntityUid idCardUid, string level, ProtoId<JobPrototype>? jobPrototype)
    {
        var accessTags = _accessSystem.TryGetTags(idCardUid);
        var currentAccess = accessTags?.ToHashSet() ?? new HashSet<ProtoId<AccessLevelPrototype>>();

        if (!TryComp<IdCardComponent>(idCardUid, out var idCardComp))
        {
            Log.Warning($"[AlertAccess] {ToPrettyString(idCardUid)} has no IdCardComponent");
            return;
        }

        var ownedTempAccess = idCardComp.TemporaryAlertAccess;
        ProtoId<AlertAccessPolicyPrototype> policyProtoId = "StationAlertAccessPolicy";

        if (!_prototypeManager.TryIndex(policyProtoId, out var policyProto))
        {
            Log.Error($"[AlertAccess] Could not find AlertAccessPolicyPrototype: '{policyProtoId}'.");
            return;
        }

        var desiredTempAccess = new HashSet<ProtoId<AccessLevelPrototype>>();

        if (policyProto.Levels.TryGetValue(level, out var levelData))
        {
            desiredTempAccess.UnionWith(levelData.TempAccess);
            AddGroupAccess(desiredTempAccess, levelData.TempAccessGroups);
            if (jobPrototype != null)
            {
                foreach (var departmentSpecific in levelData.DepartmentSpecific)
                {
                    if (!_prototypeManager.TryIndex<DepartmentPrototype>(departmentSpecific.Department, out var departmentProto))
                    {
                        Log.Warning($"[AlertAccess] Unknown department '{departmentSpecific.Department}' in policy '{policyProtoId}' level '{level}'");
                        continue;
                    }
                    if (departmentProto.Roles.Contains(jobPrototype.Value))
                    {
                        desiredTempAccess.UnionWith(departmentSpecific.TempAccess);
                        AddGroupAccess(desiredTempAccess, departmentSpecific.TempAccessGroups);
                    }
                }

                foreach (var jobSpecific in levelData.JobSpecific)
                {
                    if (jobSpecific.Job != jobPrototype.Value)
                        continue;

                    desiredTempAccess.UnionWith(jobSpecific.TempAccess);
                    AddGroupAccess(desiredTempAccess, jobSpecific.TempAccessGroups);
                }
            }
        }

        var removed = new List<ProtoId<AccessLevelPrototype>>();
        foreach (var oldAccess in ownedTempAccess.ToArray())
        {
            if (desiredTempAccess.Contains(oldAccess))
                continue;

            currentAccess.Remove(oldAccess);
            ownedTempAccess.Remove(oldAccess);
            removed.Add(oldAccess);
        }

        var added = new List<ProtoId<AccessLevelPrototype>>();
        foreach (var newAccess in desiredTempAccess)
        {
            if (currentAccess.Contains(newAccess))
                continue;

            currentAccess.Add(newAccess);
            ownedTempAccess.Add(newAccess);
            added.Add(newAccess);
        }

        if (added.Count == 0 && removed.Count == 0)
        {
            return;
        }

        var result = _accessSystem.TrySetTags(idCardUid, currentAccess);
        if (!result)
        {
            Log.Warning($"[AlertAccess] TrySetTags FAILED for {ToPrettyString(idCardUid)}");
            return;
        }

        Dirty(idCardUid, idCardComp);
        Log.Info($"[AlertAccess] {ToPrettyString(idCardUid)} (job: {jobPrototype}) at level '{level}': +[{string.Join(", ", added)}] -[{string.Join(", ", removed)}]");
    }

    ///<summary>
    /// Method that is called whenever an item is inserted into a PDA. Checks if an ID
    /// is now contained, and updates it if so.
    ///</summary>
    ///<param name="uid">The uid of the PDA</param>
    ///<param name="pda">The component of the PDA</param>
    public void UpdateTempIdAccessOnPdaInsert(EntityUid uid, PdaComponent pda)
    {
        if (pda.ContainedId == null)
            return;

        if (!TryComp<IdCardComponent>(pda.ContainedId.Value, out var idCardComp))
        {
            Log.Warning($"[AlertAccess] PDA {ToPrettyString(uid)}'s contained ID {ToPrettyString(pda.ContainedId.Value)} has no IdCardComponent");
            return;
        }

        var station = _stationSystem.GetOwningStation(uid);
        if (station == null)
        {
            Log.Warning($"[AlertAccess] Could not resolve owning station for PDA {ToPrettyString(uid)} on insert");
            return;
        }
        if (!TryComp<AlertLevelComponent>(station, out var alertComp))
        {
            Log.Warning($"[AlertAccess] Station {ToPrettyString(station.Value)} has no AlertLevelComponent");
            return;
        }

        var jobPrototype = ResolveJobPrototype(pda.ContainedId.Value, idCardComp);
        SetTemporaryAlertAccessLevel(pda.ContainedId.Value, alertComp.CurrentLevel, jobPrototype);
    }

    ///<summary>
    /// Method that is called whenever a player spawns in. Checks if they late joined,
    /// and if so updates their temporary acccess to match the current alert level.
    ///</summary>
    ///<param name="args">The arguments attached to the PlayerSpawnCompleteEvent</param>
    private void UpdateTempIdAccessOnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.LateJoin == false)
            return;

        if (!_cardSystem.TryFindIdCard(args.Mob, out var idCardUid))
        {
            Log.Warning($"[AlertAccess] Late-join {ToPrettyString(args.Mob)}: TryFindIdCard failed — no ID card found");
            return;
        }

        if (!TryComp<IdCardComponent>(idCardUid, out var idCardComp))
        {
            Log.Warning($"[AlertAccess] Late-join {ToPrettyString(args.Mob)}: resolved ID {ToPrettyString(idCardUid)} has no IdCardComponent");
            return;
        }

        var parent = Transform(idCardUid).ParentUid;
        if (parent == EntityUid.Invalid)
            return;

        if (!TryComp<PdaComponent>(parent, out var _))
            return;

        if (!_container.TryGetContainer(parent, "PDA-id", out _))
            return;

        if (!TryComp<AlertLevelComponent>(args.Station, out var alertComp))
        {
            Log.Warning($"[AlertAccess] Late-join {ToPrettyString(args.Mob)}: station {ToPrettyString(args.Station)} has no AlertLevelComponent");
            return;
        }

        Log.Info($"[AlertAccess] Late-join {ToPrettyString(args.Mob)}: applying level '{alertComp.CurrentLevel}' to ID {ToPrettyString(idCardUid)}");
        var jobPrototype = ResolveJobPrototype(idCardUid, idCardComp);
        SetTemporaryAlertAccessLevel(idCardUid, alertComp.CurrentLevel, jobPrototype);
    }


    /// <summary>
    /// Gets the Job Prototype from station records using the id card uid
    /// </summary>
    /// <param name="idCardUid">The uid of the affected id card</param>
    /// <param name="idCardComp">The component of the affected id card</param>
    private ProtoId<JobPrototype>? ResolveJobPrototype(EntityUid idCardUid, IdCardComponent idCardComp)
    {
        // Station records is the primary source of truth for Job Prototype,
        // with the idCardComp.JobPrototype as a backup
        if (TryComp<StationRecordKeyStorageComponent>(idCardUid, out var keyStorage)
            && keyStorage.Key is { } key
            && _stationRecord.TryGetRecord<GeneralStationRecord>(key, out var record))
        {
            return record.JobPrototype;
        }
        if (idCardComp.JobPrototype != null)
        {
            return idCardComp.JobPrototype;
        }

        Log.Warning($"[AlertAccess] {ToPrettyString(idCardUid)} has no findable job prototype");
        return null;
    }

    /// <summary>
    /// Expands access groups from the policy into their individual access levels
    /// and adds them to the desired temporary access. Groups that cannot be found
    /// are logged and skipped.
    /// </summary>
    /// <param name="target">The set of desired temporary access levels to add to</param>
    /// <param name="groups">The access groups to expand</param>
    private void AddGroupAccess(HashSet<ProtoId<AccessLevelPrototype>> target, List<ProtoId<AccessGroupPrototype>> groups)
    {
        foreach (var groupId in groups)
        {
            if (!_prototypeManager.TryIndex(groupId, out var group))
            {
                Log.Warning($"[AlertAccess] Access group '{groupId}' in policy not found.");
                continue;
            }
            target.UnionWith(group.Tags);
        }
    }

    #endregion
}

public sealed class AlertLevelDelayFinishedEvent : EntityEventArgs
{ }

public sealed class AlertLevelPrototypeReloadedEvent : EntityEventArgs
{ }

public sealed class AlertLevelChangedEvent : EntityEventArgs
{
    public EntityUid Station { get; }
    public string AlertLevel { get; }
    public string OldAlertLevel { get; } //Starlight

    public AlertLevelChangedEvent(EntityUid station, string alertLevel, string oldAlertLevel) // Starlight-edit: Add Old Level
    {
        Station = station;
        AlertLevel = alertLevel;
        OldAlertLevel = oldAlertLevel; // Starlight-edit: Add Old Level
    }
}
