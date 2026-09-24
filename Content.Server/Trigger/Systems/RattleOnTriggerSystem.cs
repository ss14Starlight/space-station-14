using Content.Server.Administration.Logs;
using Content.Server.Radio.EntitySystems;
using Content.Server.Pinpointer;
using Content.Shared.Mobs.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Content.Server.Chat.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.StationRecords;
using Content.Shared.Database;
using Content.Shared.PDA;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Destructible;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Trigger.Systems;

public sealed partial class RattleOnTriggerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    #region Starlight
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private StationRecordsSystem _recordsSystem = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    #endregion
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RattleOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<RattleOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        if (!TryComp<MobStateComponent>(target.Value, out var mobstate))
            return;

        args.Handled = true;

        if (!ent.Comp.Messages.TryGetValue(mobstate.CurrentState, out var messageId))
            return;

        if (ent.Comp.RadioChannel == null) //starlight
            return;

        // Gets the location of the user
        var posText = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(target.Value));
        var nameText = FormattedMessage.RemoveMarkupOrThrow(MetaData(target.Value).EntityName); // Starlight: Sanitize name

        // Starlight-start
        // Gets the job title of the user in the manifest
        var station = _station.GetOwningStation(target.Value);
        var jobName = Loc.GetString("rattle-on-trigger-job-unknown");

        if (TryComp<StationRecordsComponent>(station, out var stationRecords))
        {
            var recordId = _recordsSystem.GetRecordByName(station.Value, MetaData(target.Value).EntityName);
            if (recordId != null)
            {
                var key = new StationRecordKey(recordId.Value, station.Value);
                if (_recordsSystem.TryGetRecord<GeneralStationRecord>(key, out var entry, stationRecords))
                    jobName = !string.IsNullOrWhiteSpace(entry.JobTitle) ? entry.JobTitle : jobName;
            }
        }

        // If the manifest check didn't find a job, try to get one from their id card instead
        if (jobName.Equals(Loc.GetString("rattle-on-trigger-job-unknown")))
        {
            if (_accessReader.FindAccessItemsInventory(target.Value, out var items))
            {
                foreach (var item in items)
                {
                    // ID Card
                    if (TryComp<IdCardComponent>(item, out var id))
                    {
                        jobName = !string.IsNullOrWhiteSpace(id.LocalizedJobTitle) ? id.LocalizedJobTitle : jobName;
                    }

                    // PDA
                    if (TryComp<PdaComponent>(item, out var pda)
                        && pda.ContainedId != null
                        && TryComp(pda.ContainedId, out id))
                    {
                        jobName = !string.IsNullOrWhiteSpace(id.LocalizedJobTitle) ? id.LocalizedJobTitle : jobName;
                    }

                    if (!jobName.Equals(Loc.GetString("rattle-on-trigger-job-unknown"))) break;
                }
            }
        }
        jobName = FormattedMessage.RemoveMarkupOrThrow(jobName);
        // Starlight-end

        var message = Loc.GetString(messageId, ("user", nameText), ("job", jobName), ("position", posText)); // Starlight: Sanitized name, added job name to parameters
        // Starlight-start
        //For global announcements, ie, DAGD nuke codes, so they can't just sabotage comms. Could also use this to announce when someone important is dead.
        if (ent.Comp.Global)
        {
            var title = Loc.GetString(ent.Comp.SenderTitle);
            _chat.DispatchGlobalAnnouncement(message, title, true, ent.Comp.Sound, ent.Comp.Color);
            var actor = args.User ?? ent.Owner;
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{ToPrettyString(actor):player} has triggered the following rattle: {message}");
            return;
        }

        // Sends a message to the radio channels specified by the implant
        foreach (var radioChannel in ent.Comp.RadioChannel)
        {
            _radio.SendRadioMessage(ent.Owner, message, _prototypeManager.Index(radioChannel), ent.Owner, escapeMarkup: false); //Starlight swapped ent.Comp.RadioChannel for radioChannel, disabled escapeMarkup to allow for stylized messages
        }
        // Starlight-end
    }
}
