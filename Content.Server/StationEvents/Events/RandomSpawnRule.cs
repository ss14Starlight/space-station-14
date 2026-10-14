using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Server.Pinpointer;
using Content.Server.Radio.EntitySystems;
using Robust.Shared.Utility;

namespace Content.Server.StationEvents.Events;

public sealed partial class RandomSpawnRule : StationEventSystem<RandomSpawnRuleComponent>
{
    private const int UnobstructedTileAttempts = 50; // Starlight - Collision-safe placement.
    [Dependency] private TurfSystem _turf = default!; // Starlight

    // Moffstation - Start - Syndicate dead drop
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private RadioSystem _radio = default!;
    // Moffstation - End

    protected override void Started(EntityUid uid, RandomSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        #region Starlight
        // Attempt to find an unobstructed tile if required.
        var attempts = comp.RequireUnobstructedTile ? UnobstructedTileAttempts : 1;
        for (var i = 0; i < attempts; i++)
        {
            if (!TryFindRandomTile(out var tile, out _, out var grid, out var coords))
                continue;

            if (comp.RequireUnobstructedTile && _turf.IsTileBlocked(grid, tile, CollisionGroup.MobMask))
                continue;
            #endregion

            Sawmill.Info($"Spawning {comp.Prototype} at {coords}");
            // Moffstation - Syndicate dead drop
            var ent = Spawn(comp.Prototype, coords);

            if (comp.RadioMessage is {} radioMessage)
            {
                var message = Loc.GetString(radioMessage.Message, ("location", FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(ent))));
                _radio.SendRadioMessage(ent, message, radioMessage.Channel, ent);
            }
            // Moffstation - End
            return; // Starlight
        }

        if (comp.RequireUnobstructedTile) // Starlight
            Sawmill.Info($"No unobstructed tile found for {comp.Prototype}; skipping spawn."); // Starlight
    }
}
