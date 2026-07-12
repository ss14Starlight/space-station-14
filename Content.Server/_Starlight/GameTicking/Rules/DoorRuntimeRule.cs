using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Content.Shared.GameTicking.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.GameTicking.Rules;

/// <summary>
/// A gamerule themed around a hostile virus-like program that bolts and electrifes doors on the station.
/// </summary>
public sealed partial class DoorRuntimeRule : StationEventSystem<DoorRuntimeRuleComponent>
{
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private SharedAirlockSystem _airlock = default!;
    private const float DoorCloseBoltDelay = 0.5f;

    /// <summary>
    /// Bolts, electrifies, and turns off the safety wire of any door with AI access.
    /// </summary>
    protected override void Started(EntityUid uid, DoorRuntimeRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        EntityUid? chosenStation = null;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        chosenStation = stationEvent.TargetStation;

        if (chosenStation is null)
            if (!TryGetRandomStation(out chosenStation))
                return;

        // get all airlocks on station that are AI accessible (e.g. excludes doors with AI wire snipped)
        var airlockQuery = AllEntityQuery<DoorComponent, StationAiWhitelistComponent, TransformComponent>();

        while (airlockQuery.MoveNext(out var ent, out var doorComp, out var aiComp, out var xform))
        {
            // ensure airlock is on the target station
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
                continue;

            // skip firelocks, skip doors that the station AI cannot control (usually snipped AI access wire)
            if (HasComp<FirelockComponent>(ent) || !aiComp.Enabled)
                continue;

            // electrify all eligible doors
            if (TryComp<ElectrifiedComponent>(ent, out var electrified))
                _electrocution.SetElectrified((ent, electrified), true);

            // turn off the airlock safety - we want to force doors to close (even if people are present)
            if (TryComp<AirlockComponent>(ent, out var airlockComp) && airlockComp.Powered)
                _airlock.SetSafety(airlockComp, false);

            // bolt all eligible doors
            if (TryComp<DoorBoltComponent>(ent, out var boltComp))
            {
                // bolt doors if welded or closed; if doors are open, try to close them before bolting
                if (doorComp.State is DoorState.Welded or DoorState.Closed)
                    _door.SetBoltsDown((ent, boltComp), true);
                else if (_door.TryClose(ent, doorComp))
                    Timer.Spawn(TimeSpan.FromSeconds(DoorCloseBoltDelay), () => _door.SetBoltsDown((ent, boltComp), true));
            }

            // add to list so we can undo all of this later
            comp.AffectedEntities.Add(ent);
        }
    }

    /// <summary>
    /// Unbolts, unelectrifies, and restores the safety of all of the doors affected by the event.
    /// </summary>
    protected override void Ended(EntityUid uid, DoorRuntimeRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        // run through the list to undo the gamerule
        foreach (var ent in comp.AffectedEntities)
        {
            // restore door safety - we want to do this even if the door isn't powered to avoid an edge-case
            // e.g. door safety is turned off when door is powered, then door loses power before the end of the event
            if (TryComp<AirlockComponent>(ent, out var airlockComp))
                _airlock.SetSafety(airlockComp, true);

            // unelectrify door
            if (TryComp<ElectrifiedComponent>(ent, out var electrified))
                _electrocution.SetElectrified((ent, electrified), false);

            // unbolt door
            if (TryComp<DoorBoltComponent>(ent, out var boltComp))
                _door.SetBoltsDown((ent, boltComp), false);
        }

        comp.AffectedEntities.Clear();
    }
}
