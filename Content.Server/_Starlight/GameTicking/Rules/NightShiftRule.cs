using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.AlertLevel;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Station.Components;
using Robust.Shared.Player;

namespace Content.Server.StationEvents.Events;

public sealed class NightShiftRule : StationEventSystem<NightShiftRuleComponent>
{
    [Dependency] private readonly SharedPoweredLightSystem _poweredLightSystem = default!;

    protected override void Started(EntityUid uid, NightShiftRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        EntityUid? chosenStation = null;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        chosenStation = stationEvent.TargetStation;
        if (chosenStation is null)
            if (!TryGetRandomStation(out chosenStation))
                return;

        if (TryComp<AlertLevelComponent>(chosenStation, out var alert) && !(alert.CurrentLevel == "green" || alert.CurrentLevel == "blue"))
        {
            if(comp.Announcement is {} locId)
                Announce(stationEvent, Loc.GetString(locId), true);

            return;
        }

        var query = AllEntityQuery<PoweredLightComponent>();
        while (query.MoveNext(out var ent, out var light))
            _poweredLightSystem.SetNightMode(ent, true, light);
    }

    protected override void Ended(EntityUid uid, NightShiftRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        EntityUid? chosenStation = null;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        chosenStation = stationEvent.TargetStation;

        var query = AllEntityQuery<PoweredLightComponent>();
        while (query.MoveNext(out var ent, out var light))
            _poweredLightSystem.SetNightMode(ent, false, light);
    }
}
