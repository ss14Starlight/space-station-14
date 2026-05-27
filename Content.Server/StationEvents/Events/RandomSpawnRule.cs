using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
// Moffstation - Start - Syndicate dead drop
using Content.Server.Radio.EntitySystems;
using Content.Server.Pinpointer;
using Robust.Shared.Utility;
// Moffstation - End

namespace Content.Server.StationEvents.Events;

public sealed class RandomSpawnRule : StationEventSystem<RandomSpawnRuleComponent>
{
    // Moffstation - Start - Syndicate dead drop
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    // Moffstation - End

    protected override void Started(EntityUid uid, RandomSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (TryFindRandomTile(out _, out _, out _, out var coords))
        {
            Sawmill.Info($"Spawning {comp.Prototype} at {coords}");
            // Moffstation - Syndicate dead drop
            var ent = Spawn(comp.Prototype, coords);

            if (comp.RadioMessage is {} radioMessage)
            {
                var message = Loc.GetString(radioMessage.Message, ("location", FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(ent))));
                _radio.SendRadioMessage(ent, message, radioMessage.Channel, ent);
            }
            // Moffstation - End
		}
    }
}
