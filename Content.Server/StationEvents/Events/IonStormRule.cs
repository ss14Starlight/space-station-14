using Content.Server.Silicons.Laws;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Station.Components;
using Content.Server._Starlight.Thaven; //Starlight
using Content.Shared._Starlight.Thaven.Components; //Starlight 

namespace Content.Server.StationEvents.Events;

public sealed class IonStormRule : StationEventSystem<IonStormRuleComponent>
{
    [Dependency] private readonly IonStormSystem _ionStorm = default!;
    [Dependency] private readonly ThavenMoodsSystem _thavenMood = default!; //Starlight

    protected override void Started(EntityUid uid, IonStormRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        //Starlight begin | Prefer target station if there is one, if SOMEHOW that odesn't exist, fallback to existing trygetrandomstation call
        EntityUid? chosenStation = null;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        chosenStation = stationEvent.TargetStation;
        if (chosenStation is null)
            if (!TryGetRandomStation(out chosenStation))
                return;
        //Starlight end

        var query = EntityQueryEnumerator<SiliconLawBoundComponent, TransformComponent, IonStormTargetComponent>();
        while (query.MoveNext(out var ent, out var lawBound, out var xform, out var target))
        {
            // only affect law holders on the station
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
                continue;

            _ionStorm.IonStormTarget((ent, lawBound, target));
        }

        //Starlight begin | Ion storm affects Thaven moods
        var moodsQuery = EntityQueryEnumerator<ThavenMoodsComponent, TransformComponent>();
        while (moodsQuery.MoveNext(out var ent, out var moodHolder, out var xform))
        {            
            // only affect Thaven Moods holders on the station
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != chosenStation)
                continue;
        
            _thavenMood.OnIonStorm((ent, moodHolder));
        }
        //Startlight end
    }
}
