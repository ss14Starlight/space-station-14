// ReSharper disable CheckNamespace

using Content.Server._Starlight.Station;
using Content.Server.NPC;
using Content.Server.Station.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Dragon;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Sprite;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Server.Dragon;

public sealed partial class DragonRiftSystem
{
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationCrewCountSystem _crewCount = default!;

    private static readonly EntProtoId _sharkMinnowPrototype = "RiftSharkminnow";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DragonRiftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {

            if (comp.State != DragonRiftState.Finished && comp.Accumulator >= comp.MaxAccumulator)
            {
                // TODO: When we get autocall you can buff if the rift finishes / 3 rifts are up
                // for now they just keep 3 rifts up.

                if (comp.Dragon != null)
                    _dragon.RiftCharged(comp.Dragon.Value);

                comp.Accumulator = comp.MaxAccumulator;
                RemComp<DamageableComponent>(uid);
                comp.State = DragonRiftState.Finished;
                Dirty(uid, comp);
            }
            else if (comp.State != DragonRiftState.Finished)
            {
                comp.Accumulator += frameTime;
            }

            comp.SpawnAccumulator += frameTime;

            if (comp.State < DragonRiftState.AlmostFinished && comp.Accumulator > comp.MaxAccumulator / 2f)
            {
                comp.State = DragonRiftState.AlmostFinished;
                // Only Dragon Rifts get the guaranteed SharkMinnow.
                if (comp.Dragon != null)
                {
                    comp.SpawnAccumulator = 0f; // Reset spawn timer after the guaranteed Sharkminnow spawn
                    Dirty(uid, comp);
                }

                var closestStation = _station.GetNearestStation(uid, true);
                if (closestStation.Owner != EntityUid.Invalid)
                {
                    var msg = Loc.GetString("carp-rift-warning",
                        ("location",
                            FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString((uid, xform)))),
                        ("station", MetaData(closestStation.Owner).EntityName));
                    _chat.DispatchGlobalAnnouncement(msg, playSound: false, colorOverride: Color.Red);
                    _audio.PlayGlobal(new ResolvedPathSpecifier("/Audio/Misc/notice1.ogg"), Filter.Broadcast(), true);
                    _navMap.SetBeaconEnabled(uid, true);
                    if (TryComp<NavMapBeaconComponent>(uid, out var beacon))
                        _navMap.RefreshNavMapBeacon((uid, beacon));
                }

                // Spawn the guaranteed 50% SharkMinnow only for Dragon Rifts
                if (comp.Dragon != null)
                {
                    var sharkminnow = Spawn(_sharkMinnowPrototype, xform.Coordinates);

                    if (TryComp<DragonComponent>(comp.Dragon.Value, out var dragon))
                        dragon.SharkMinnows.Add(sharkminnow);
                }
            }

            if (comp.SpawnAccumulator > comp.SpawnCooldown)
            {
                comp.SpawnAccumulator -= comp.SpawnCooldown;

                var spawnPrototype = comp.SpawnPrototype;
                var isSharkminnow = false;

                // Only Dragon Rifts with a linked Dragon use the SharkMinnow spawn roll.
                if (comp.Dragon != null)
                {
                    var totalCrewCount = _crewCount.GetTotalCrewCount();
                    comp.SharkMinnowLimit = totalCrewCount / 12;
                    Dirty(uid, comp);

                    var canSpawnSharkminnow = true;

                    if (TryComp<DragonComponent>(comp.Dragon.Value, out var dragon))
                    {
                        CleanupSharkMinnows(dragon);

                        if (dragon.SharkMinnows.Count >= comp.SharkMinnowLimit)
                            canSpawnSharkminnow = false;
                    }

                    var finishedMultiplier = comp.State == DragonRiftState.Finished ? 1 : 0;
                    var sharkChance = 2 * (1 + finishedMultiplier); // 1/50 unfinished, 1/25 finished

                    var roll = _random.Next(1, 101);

                    if (roll <= sharkChance && canSpawnSharkminnow)
                    {
                        spawnPrototype = _sharkMinnowPrototype;
                        isSharkminnow = true;
                    }
                }

                // Non-SharkMinnow spawns inherit the Dragon's appearance.
                var ent = Spawn(spawnPrototype, xform.Coordinates);

                if (isSharkminnow && comp.Dragon != null && TryComp<DragonComponent>(comp.Dragon.Value, out var dragonComp))
                    dragonComp.SharkMinnows.Add(ent);

                if (!isSharkminnow &&
                    comp.Dragon is { } dragonUid &&
                    TryComp<RandomSpriteComponent>(dragonUid, out var randomSprite))
                {
                    var spawnedSprite = EnsureComp<RandomSpriteComponent>(ent);
                    _serManager.CopyTo(randomSprite, ref spawnedSprite, notNullableOverride: true);
                    Dirty(ent, spawnedSprite);
                }

                // SharkMinnows do not follow the Dragon.
                if (!isSharkminnow && comp.Dragon != null)
                {
                    _npc.SetBlackboard(ent, NPCBlackboard.FollowTarget,
                        new EntityCoordinates(comp.Dragon.Value, Vector2.Zero));
                }
            }
        }
    }

    private void OnGetState(Entity<DragonRiftComponent> ent, ref ComponentGetState args) =>
    args.State = new DragonRiftComponentState
    {
        State = ent.Comp.State,
        SharkMinnowLimit = ent.Comp.SharkMinnowLimit,
    };

    private void CleanupSharkMinnows(DragonComponent dragon) =>
        dragon.SharkMinnows.RemoveWhere(sharkminnow =>
            !Exists(sharkminnow) ||
            !TryComp<MobStateComponent>(sharkminnow, out var mobState) ||
            mobState.CurrentState == MobState.Dead);
}
