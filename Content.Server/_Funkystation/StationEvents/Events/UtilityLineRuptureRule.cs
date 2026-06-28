using Content.Server.Chat.Systems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Pinpointer;
using Content.Server.StationEvents.Events;
using Content.Server._Funkystation.StationEvents.Components;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Random;

namespace Content.Server._Funkystation.StationEvents.Events;

public sealed partial class UtilityLineRuptureRule : StationEventSystem<UtilityLineRuptureRuleComponent>
{
    [Dependency] private NavMapSystem _navMap = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private ChatSystem _chat = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private PuddleSystem _puddle = null!;
    [Dependency] private IRobustRandom _random = null!;

    protected override void Started(EntityUid uid, UtilityLineRuptureRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Pick a reagent from the list
        if (component.PossibleReagents.Count > 0)
        {
            component.Reagent = _random.Pick(component.PossibleReagents);
        }

        if (!TryFindRandomTile(out _, out var targetStation, out _, out var targetCoords))
            return;

        component.TargetCoordinates = targetCoords;
        component.RuptureTime = _timing.CurTime + TimeSpan.FromSeconds(10);

        var mapCoords = _transform.ToMapCoordinates(targetCoords);
        var locationName = FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString(mapCoords));

        {
            // Announce 10 seconds before it happens (if you weren't paying attention you get round removed bye)
            var msg = Loc.GetString("utility-line-rupture-announcement", ("location", locationName));
            _chat.DispatchStationAnnouncement(targetStation.Value, msg, Loc.GetString("utility-line-rupture-sender"), playDefaultSound: true, colorOverride: Color.FromHex("#f9a524"));
        }
    }

    protected override void ActiveTick(EntityUid uid, UtilityLineRuptureRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.RuptureTime == null || _timing.CurTime < component.RuptureTime)
            return;

        component.RuptureTime = null;

        if (component.TargetCoordinates is not { } coords)
            return;

        // Set up the large spill
        var solution = new Solution();
        solution.AddReagent(component.Reagent, component.SpillVolume);

        if (_puddle.TrySpillAt(coords, solution, out var puddleUid))
        {
            // Ignite yum yum yum
            var fireEv = new TileFireEvent(1000f, 100f);
            RaiseLocalEvent(puddleUid, ref fireEv);
        }
    }
}
