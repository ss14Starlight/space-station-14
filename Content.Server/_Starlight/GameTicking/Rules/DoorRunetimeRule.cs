using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Content.Shared.GameTicking.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Robust.Shared.Timing;
namespace Content.Server.StationEvents.Events;

public sealed class DoorRunetimeRule : StationEventSystem<DoorRunetimeRuleComponent>
{
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly SharedAirlockSystem _airlock = default!;

    protected override void Started(EntityUid uid, DoorRunetimeRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        EntityUid? chosenStation = null;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        chosenStation = stationEvent.TargetStation;
        if (chosenStation is null)
            if (!TryGetRandomStation(out chosenStation))
                return;

        var airlockQuery = AllEntityQuery<DoorComponent, StationAiWhitelistComponent, TransformComponent>();
        while (airlockQuery.MoveNext(out var ent, out var doorComp, out var aiComp, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
                continue;

            if (HasComp<FirelockComponent>(ent) || !aiComp.Enabled)
                continue;

            if (TryComp<AirlockComponent>(ent, out var airlockComp))
            {
                if (!airlockComp.Powered)
                    continue;

                _airlock.SetSafety(airlockComp, false);
            }

            if (TryComp<DoorBoltComponent>(ent, out var boltComp))
                if (doorComp.State is DoorState.Welded or DoorState.Closed)
                    _door.SetBoltsDown((ent, boltComp), true);
                else if (_door.TryClose(ent, doorComp))
                    Timer.Spawn(TimeSpan.FromSeconds(0.5f), () => _door.SetBoltsDown((ent, boltComp), true));
            else
                {
                    if (TryComp<AirlockComponent>(ent, out var airlockComp2))
                        _airlock.SetSafety(airlockComp2, true);

                    continue;
                }

            if (TryComp<ElectrifiedComponent>(ent, out var electrified))
                _electrocution.SetElectrified((ent, electrified), true);

            comp.AffectedEntities.Add(ent);
        }
    }

    protected override void Ended(EntityUid uid, DoorRunetimeRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        foreach (var ent in comp.AffectedEntities)
        {
            if (TryComp<StationAiWhitelistComponent>(ent, out var aiComp) && !aiComp.Enabled)
                continue;

            if (TryComp<AirlockComponent>(ent, out var airlockComp))
            {
                if (!airlockComp.Powered)
                    continue;

                _airlock.SetSafety(airlockComp, true);
            }

            if (TryComp<DoorBoltComponent>(ent, out var boltComp))
                _door.SetBoltsDown((ent, boltComp), false);

            _door.TryOpen(ent);

            if (TryComp<ElectrifiedComponent>(ent, out var electrified))
                _electrocution.SetElectrified((ent, electrified), false);
        }

        comp.AffectedEntities.Clear();
    }
}
