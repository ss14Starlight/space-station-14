// ReSharper disable CheckNamespace

using Content.Server._Starlight.Station;
using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Server.Pinpointer;
using Content.Server.Station.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Dragon;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Examine;
using Content.Shared.Sprite;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
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
                    _audio.PlayGlobal("/Audio/Misc/notice1.ogg", Filter.Broadcast(), true);
                    _navMap.SetBeaconEnabled(uid, true);
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
                var rareSpawn = false;
                var isSharkminnow = false;

                // Only Dragon Rifts with an linked Dragon use the special SharkMinnow/HoloCarp spawn rolls.
                if (comp.Dragon != null)
                {

                    var totalCrewCount = _crewCount.GetTotalCrewCount();
                    comp.SharkMinnowLimit = totalCrewCount / 2;
                    Dirty(uid, comp);

                    var canSpawnSharkminnow = true;

                    if (TryComp<DragonComponent>(comp.Dragon.Value, out var dragon))
                    {
                        CleanupSharkMinnows(dragon);

                        var sharkMinnowCount = dragon.SharkMinnows.Count;

                        if (sharkMinnowCount >= comp.SharkMinnowLimit)
                            canSpawnSharkminnow = false;
                    }

                    var finishedMultiplier = comp.State == DragonRiftState.Finished ? 1 : 0;
                    var rareChance = 20 * (1 + finishedMultiplier);
                    var sharkChance = 5 * (1 + finishedMultiplier);

                    var roll = _random.Next(1, 101);

                    if (roll <= rareChance)
                    {
                        rareSpawn = true;

                        if (roll <= sharkChance && canSpawnSharkminnow)
                        {
                            spawnPrototype = _sharkMinnowPrototype;
                            isSharkminnow = true;
                        }
                        else
                        {
                            spawnPrototype = new EntProtoId("RiftCarpHolo");
                        }
                    }
                }

                // Non-Dragon Rifts simply spawn their configured prototype.
                var ent = Spawn(spawnPrototype, xform.Coordinates);

                if (isSharkminnow && comp.Dragon != null && TryComp<DragonComponent>(comp.Dragon.Value, out var dragonComp))
                    dragonComp.SharkMinnows.Add(ent);

                // Update their look to match the leader.
                if (!rareSpawn && TryComp<RandomSpriteComponent>(comp.Dragon, out var randomSprite))
                {
                    var spawnedSprite = EnsureComp<RandomSpriteComponent>(ent);
                    _serManager.CopyTo(randomSprite, ref spawnedSprite, notNullableOverride: true);
                    Dirty(ent, spawnedSprite);
                }

                // Sharkminnows do not follow the Dragon.
                if (!isSharkminnow && comp.Dragon != null)
                {
                    _npc.SetBlackboard(ent, NPCBlackboard.FollowTarget,
                        new EntityCoordinates(comp.Dragon.Value, Vector2.Zero));
                }
            }
        }
    }

    private void OnGetState(Entity<DragonRiftComponent> ent, ref ComponentGetState args)
    {
        args.State = new DragonRiftComponentState
        {
            State = ent.Comp.State,
            SharkMinnowLimit = ent.Comp.SharkMinnowLimit,
        };
    }

    private void CleanupSharkMinnows(DragonComponent dragon)
    {
        dragon.SharkMinnows.RemoveWhere(sharkminnow =>
            !Exists(sharkminnow) ||
            !TryComp<MobStateComponent>(sharkminnow, out var mobState) ||
            mobState.CurrentState == MobState.Dead);
    }
}
