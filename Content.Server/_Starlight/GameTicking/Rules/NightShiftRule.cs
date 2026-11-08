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

    /// <summary>
    /// Forces night shift on the target station, choosing a random station if no target is set.
    /// </summary>
    protected override void Started(EntityUid uid, NightShiftRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        var station = CompOrNull<StationEventComponent>(uid)?.TargetStation;
        if (station == null && !TryGetRandomStation(out station))
            return;

        comp.Station = station;
        _nightShift.SetForced(station.Value, uid, true);
    }

    /// <summary>
    /// Releases this rule's forced night shift when the event ends.
    /// </summary>
    protected override void Ended(EntityUid uid, NightShiftRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);

        Release((uid, comp));
    }

    /// <summary>
    /// Releases this rule's forced night shift when its component shuts down.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnShutdown(Entity<NightShiftRuleComponent> ent, ref ComponentShutdown args)
        => Release(ent);

    /// <summary>
    /// Clears the stored station and removes this rule's forced source if the station still exists.
    /// </summary>
    private void Release(Entity<NightShiftRuleComponent> ent)
    {
        if (ent.Comp.Station is not { } station)
            return;

        ent.Comp.Station = null;
        if (!TerminatingOrDeleted(station))
            _nightShift.SetForced(station, ent, false);
    }
}
