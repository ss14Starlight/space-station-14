using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server._Starlight.Light.NightShift;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;

namespace Content.Server._Starlight.GameTicking.Rules;

/// <summary>
/// Forces the station's night shift for the duration of the event, see <see cref="NightShiftSystem"/>.
/// </summary>
public sealed partial class NightShiftRule : StationEventSystem<NightShiftRuleComponent>
{
    [Dependency] private NightShiftSystem _nightShift = default!;

    protected override void Started(EntityUid uid, NightShiftRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        var station = CompOrNull<StationEventComponent>(uid)?.TargetStation;
        if (station == null && !TryGetRandomStation(out station))
            return;

        comp.Station = station;
        _nightShift.SetForced(station.Value, uid, true);
    }

    protected override void Ended(EntityUid uid, NightShiftRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        if (comp.Station is { } station && !TerminatingOrDeleted(station))
            _nightShift.SetForced(station, uid, false);
    }
}
