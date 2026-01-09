using Content.Server.Ghost.Roles.Events;
using Content.Server.Popups;
using Content.Shared._Starlight.Doomed;
using Content.Shared._Starlight.Friendship;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.Friendship;

public sealed partial class FriendshipFamiliarSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FriendshipFamiliarComponent, SummonFriendInstantEvent>(OnSummonFriend);
        SubscribeLocalEvent<FriendshipFamiliarMobComponent, GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
    }

    private void OnSummonFriend(Entity<FriendshipFamiliarComponent> ent, ref SummonFriendInstantEvent args)
    {
        if (ent.Comp.SpawnedMob is EntityUid spawnedMob)
        {
            DoomedComponent doomed = new()
            {
                TimeToDeath = TimeSpan.FromSeconds(10),
                DamageEffect = "Acidifier"     
            };
            AddComp(spawnedMob, doomed);
            _popup.PopupEntity(Loc.GetString("friendship-familiar-death-soon", ("name", Name(spawnedMob))), spawnedMob, PopupType.LargeCaution);

            ent.Comp.SpawnedMob = null;

            // intentionally don't set handled to true here, we don't want cooldown for deleting a mob
        }
        else
        {
            var spawnPoint = Spawn(ent.Comp.MobToSpawn, Transform(args.Performer).Coordinates);
            _transform.SetParent(spawnPoint, ent.Owner);
            
            args.Handled = true;
        }  
    }

    private void OnSpawnerUsed(Entity<FriendshipFamiliarMobComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        var parent = Transform(args.Spawner).ParentUid;
        if (!TryComp<FriendshipFamiliarComponent>(parent, out var friendshipFamiliarComp) || friendshipFamiliarComp.SpawnedMob is EntityUid uid)
        {
            QueueDel(ent.Owner);
            return;
        }

        friendshipFamiliarComp.SpawnedMob = args.Spawned;
    }
}

public sealed partial class SummonFriendInstantEvent : InstantActionEvent;