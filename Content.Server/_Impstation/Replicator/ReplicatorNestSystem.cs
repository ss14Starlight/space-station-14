// these are HEAVILY based on the Bingle free-agent ghostrole from GoobStation, but reflavored and reprogrammed to make them more Robust (and less of a meme.)
// all credit for the core gameplay concepts and a lot of the core functionality of the code goes to the folks over at Goob, but I re-wrote enough of it to justify putting it in our filestructure.
// the original Bingle PR can be found here: https://github.com/Goob-Station/Goob-Station/pull/1519

using Content.Server._Impstation.Administration.Components;
using Content.Server.Actions;
using Content.Server.Audio;
using Content.Server.Buckle.Systems;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Server.Stunnable;
using Content.Shared._Impstation.Replicator;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Buckle.Components;
using Content.Shared.Destructible;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using Content.Server._Moffstation.Chasm;
using Content.Server.Chat.Systems;
using Content.Server.Item;
using Content.Server.Mind;
using Content.Shared._Impstation.SpawnedFromTracker;
using Content.Shared.Construction.Components;
using Content.Shared.Humanoid;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.Audio;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Impstation.Replicator;

public sealed class ReplicatorNestSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PinpointerSystem _pinpointer = default!;
    [Dependency] private readonly AmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly ItemSystem _item = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly ReplicatorSystem _replicator = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorNestComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReplicatorNestComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<ReplicatorNestComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
        SubscribeLocalEvent<ReplicatorNestComponent, ChasmFallEvent>(OnFall);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorUpgradeActionEvent>(OnUpgrade);
    }

    private void OnFall(Entity<ReplicatorNestComponent> ent, ref ChasmFallEvent args)
    {
        if (!ent.Comp.HasAnnounced && ent.Comp.CurrentLevel >= ent.Comp.AnnounceAtLevel)
        {
            ent.Comp.HasAnnounced = true;
            _chatSystem.DispatchStationAnnouncement(ent, ent.Comp.Announcement, colorOverride: Color.Red);
        }

        UpdatePoints(ent, args.Tripper);
        HandlePoints(ent);
    }

    private void UpdatePoints(Entity<ReplicatorNestComponent> ent, EntityUid tripper) // this is its own method because I think it reads cleaner. also the way goobcode handled this sucked.
    {
        // regardless of what falls in, you get at least one point
        if (!HasComp<StackComponent>(tripper)) // as long as it's not a stack.
        {
            ent.Comp.TotalPoints += 10;
            ent.Comp.SpawningProgress += 10;
        }

        // if the item is in a stack, you get points depending on how many items are in that stack.
        if (TryComp<StackComponent>(tripper, out var stackComp))
        {
            ent.Comp.TotalPoints += stackComp.Count;
            ent.Comp.SpawningProgress += stackComp.Count;
        }

        // you get a bonus point if the item is Large, 2 bonus points if it's Huge, and 3 bonus points if it's above that.
        else if (TryComp<ItemComponent>(tripper, out var itemComp))
        {
            var weight = _item.GetSizePrototype(itemComp.Size).Weight;
            if (weight > _item.GetSizePrototype("Large").Weight)
                ModifyPoints(ent, 10);
            if (weight > _item.GetSizePrototype("Huge").Weight)
                ModifyPoints(ent, 10);
            if (weight >= _item.GetSizePrototype("Ginormous").Weight)
                ModifyPoints(ent, 10);
            // regardless, items only net 1 spawning progress.
            ModifyPoints(ent, 10);
        }

        // if it wasn't an item and was anchorable, you get 3 bonus points.
        else if (HasComp<AnchorableComponent>(tripper))
        {
            ModifyPoints(ent, 30);
        }

        // now we handle points if it *isn't* a replicator, structure, or item, but *is* a living thing
        else if (HasComp<MobStateComponent>(tripper))
        {
            // bonus points for humanoid (default 2) times current level plus enough progress for one new replicator
            if (HasComp<HumanoidAppearanceComponent>(tripper))
                ModifyPoints(ent, (10 * ent.Comp.CurrentLevel) + ent.Comp.SpawnNewAt);
            // otherwise, you get bonus points for living (default 1) times current level and 1/4th progress
            else
                ModifyPoints(ent, (5 * ent.Comp.CurrentLevel) + (ent.Comp.SpawnNewAt / 4));
        }
    }

    private void HandlePoints(Entity<ReplicatorNestComponent> ent)
    {
        // if we exceed the upgrade threshold after points are added,
        if (ent.Comp.TotalPoints >= ent.Comp.NextUpgradeAt)
        {
            // level up
            ent.Comp.CurrentLevel++;

            // this allows us to have an arbitrary number of unique messages for when the nest levels up - and a default for if we run out.
            var growthMessage = $"replicator-nest-level{ent.Comp.CurrentLevel}";
            _popup.PopupEntity(Loc.TryGetString(growthMessage, out var localizedMsg)
                    ? localizedMsg
                    : Loc.GetString("replicator-nest-levelup"),
                ent);

            // make the nest sprite grow as long as we have sprites for it. I am NOT scaling it.
            if (ent.Comp.CurrentLevel <= ent.Comp.EndgameLevel)
            {
                Embiggen(ent);
            }

            // update the threshold for the next upgrade (the default times the current level), and upgrade all our guys.
            // threshold increases plateau at the endgame level.
            ent.Comp.NextUpgradeAt += ent.Comp.CurrentLevel >= ent.Comp.EndgameLevel
                ? ent.Comp.UpgradeAt * ent.Comp.EndgameLevel
                : ent.Comp.UpgradeAt * ent.Comp.CurrentLevel;
            UpgradeAll(ent);
            _audio.PlayPvs(ent.Comp.LevelUpSound, ent);

            // increase the radius at which tiles are converted.
            ent.Comp.TileConversionRadius += ent.Comp.TileConversionIncrease;

            // and increase the radius of the ambient nest sound
            if (TryComp<AmbientSoundComponent>(ent.Comp.PointsStorage, out var ambientComp))
                _ambientSound.SetRange(ent.Comp.PointsStorage, ambientComp.Range + 1, ambientComp);
        }

        // after upgrading, if we exceed the next spawn threshold, spawn a new (un-upgraded) replicator, then set the next spawn threshold.
        if (ent.Comp.SpawningProgress >= ent.Comp.NextSpawnAt)
        {
            SpawnNew(ent);
            ent.Comp.SpawningProgress = 0;
        }

        // then convert some tiles
        if (ent.Comp.TotalPoints >= ent.Comp.NextTileConvertAt)
        {
            ConvertTiles(ent, ent.Comp.TileConversionRadius);
            ent.Comp.NextTileConvertAt += ent.Comp.TileConvertAt;
        }

        // and dirty so the client knows if it's supposed to update the nest visuals
        Dirty(ent);

        // finally, update the PointsStorage entity.
        var pointsStorageComponent = EnsureComp<ReplicatorNestPointsStorageComponent>(ent.Comp.PointsStorage);

        pointsStorageComponent.Level = ent.Comp.CurrentLevel;
        pointsStorageComponent.TotalPoints = ent.Comp.TotalPoints;
        pointsStorageComponent.TotalReplicators = ent.Comp.SpawnedMinions.Count;
    }

    private void ModifyPoints(Entity<ReplicatorNestComponent> ent, int points)
    {
        ent.Comp.TotalPoints += points;
        ent.Comp.SpawningProgress += points;
    }

    private void Embiggen(Entity<ReplicatorNestComponent> ent)
    {
        RaiseNetworkEvent(new ReplicatorNestEmbiggenedEvent(ent));
    }

    private void SpawnNew(Entity<ReplicatorNestComponent> ent)
    {
        // spawn a new replicator
        var spawner = Spawn(ent.Comp.ToSpawn, Transform(ent).Coordinates);
        // TODO:
        //OnSpawnTile(ent, ent.comp.Level * 2, "FloorReplicator");

        // make sure our new GhostRoleSpawnPoint knows where it came from, so it can pass that down to the replicator it spawns.
        var tracker = EnsureComp<SpawnedFromTrackerComponent>(spawner);
        tracker.SpawnedFrom = ent;

        ent.Comp.UnclaimedSpawners.Add(spawner);
    }

    public void UpgradeAll(Entity<ReplicatorNestComponent> ent)
    {
        foreach (var replicator in ent.Comp.SpawnedMinions)
        {
            if (!TryComp<ReplicatorComponent>(replicator, out var comp) || comp.UpgradeActions.Count == 0)
                continue;

            if (comp.HasBeenGivenUpgradeActions)
                continue;

            var hasMind = _mind.TryGetMind(replicator, out var mind, out _);

            foreach (var action in comp.UpgradeActions)
            {
                if (hasMind)
                    _actions.AddAction(replicator, action);
                else
                    _actionContainer.AddAction(mind, action);
            }
            comp.HasBeenGivenUpgradeActions = true;
        }
    }

    private void ConvertTiles(Entity<ReplicatorNestComponent> ent, float radius)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? mapGrid))
            return;

        var tileEnumerator = _map.GetLocalTilesEnumerator(gridUid,
            mapGrid,
            new Box2(xform.Coordinates.Position + new System.Numerics.Vector2(-radius, -radius),
            xform.Coordinates.Position + new System.Numerics.Vector2(radius, radius)));

        var convertTile = (ContentTileDefinition)_tileDef[ent.Comp.ConversionTile];

        while (tileEnumerator.MoveNext(out var tile))
        {
            if (tile.Tile.TypeId == convertTile.TileId)
                continue;

            var tileCoords = tile.GridIndices;
            var nestCoords = xform.Coordinates.Position;

            // have to check the distance from nest center to tileref, otherwise it comes out square due to Box2
            if (Math.Sqrt(Math.Pow(tileCoords.X - (nestCoords.X - 0.5), 2) + Math.Pow(tileCoords.Y - (nestCoords.Y - 0.5), 2)) >= radius)
                continue;

            if (_random.Prob(ent.Comp.TileConversionChance))
            {
                var center = _turf.GetTileCenter(tile);

                Spawn(ent.Comp.TileConversionVfx, center);
                _audio.PlayPvs(ent.Comp.TilePlaceSound, center);

                _tile.ReplaceTile(tile, convertTile);
                _tile.PickVariant(convertTile);
            }
        }
    }

    public void OnUpgrade(Entity<ReplicatorComponent> ent, ref ReplicatorUpgradeActionEvent args)
    {
        var nextStage = args.NextStage;

        if (ent.Comp.MyNest == null || _replicator.UpgradeReplicator(ent, nextStage) is not { } upgraded)
        {
            _popup.PopupEntity(Loc.GetString("replicator-cant-find-nest"), ent, PopupType.MediumCaution);
            return;
        }

        upgraded.Comp.RelatedReplicators = ent.Comp.RelatedReplicators;
        upgraded.Comp.MyNest = ent.Comp.MyNest;

        QueueDel(ent);
        foreach (var action in ent.Comp.Actions)
        {
            QueueDel(action);
        }

        _popup.PopupEntity(Loc.GetString($"{ent.Comp.ReadyToUpgradeMessage}-others"), ent, PopupType.MediumCaution);
    }

    private void OnEntRemoved(Entity<ReplicatorNestComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        RemCompDeferred<StunnedComponent>(args.Entity);
    }

    private void OnMapInit(Entity<ReplicatorNestComponent> ent, ref MapInitEvent args)
    {
        if (!Transform(ent).Coordinates.IsValid(EntityManager))
            QueueDel(ent);

        ent.Comp.NextSpawnAt = ent.Comp.SpawnNewAt;
        ent.Comp.NextUpgradeAt = ent.Comp.UpgradeAt;
        ent.Comp.NextTileConvertAt = ent.Comp.TileConvertAt;

        var pointsStorageEnt = Spawn("ReplicatorNestPointsStorage", Transform(ent).Coordinates);
        EnsureComp<ReplicatorNestPointsStorageComponent>(pointsStorageEnt);

        ent.Comp.PointsStorage = pointsStorageEnt;
    }

    private void OnDestroyed(Entity<ReplicatorNestComponent> ent, ref DestructionEventArgs args)
    {
        HandleDestruction(ent);
    }

    private void HandleDestruction(Entity<ReplicatorNestComponent> ent)
    {
        // delete all unclaimed spawners
        foreach (var spawner in ent.Comp.UnclaimedSpawners)
        {
            ent.Comp.UnclaimedSpawners.Remove(spawner);
            QueueDel(spawner);
        }

        // Figure out who the queen is & which replicators belonging to this nest are still alive.
        EntityUid? queen = null;
        HashSet<Entity<ReplicatorComponent>> livingReplicators = [];
        foreach (var replicator in ent.Comp.SpawnedMinions)
        {
            if (!TryComp<ReplicatorComponent>(replicator, out var replicatorComp))
                continue;

            if (!_mobState.IsAlive(replicator))
                continue;

            replicatorComp.MyNest = null;

            if (replicatorComp.Queen)
                queen = replicator;

            livingReplicators.Add((replicator, replicatorComp));

            _popup.PopupEntity(Loc.GetString("replicator-nest-destroyed"), replicator, replicator, Shared.Popups.PopupType.LargeCaution);
        }

        // if there are living replicators, select one and give the action to create a new nest.
        if (livingReplicators.Count > 0)
        {
            // if queen isn't null, assign it to queenNotNull. if it is, pick a random EntityUid from the list and assign it to queenNotNull
            if (queen is not { } queenNotNull)
                queenNotNull = _random.Pick(livingReplicators);

            var comp = EnsureComp<ReplicatorComponent>(queenNotNull);
            comp.Queen = true;
            livingReplicators.Add((queenNotNull, comp));
            foreach (var replicator in livingReplicators)
            {
                comp.RelatedReplicators.Add(GetNetEntity(replicator)); // make sure we know who belongs to our nest
            }

            var upgradedQueen = _replicator.UpgradeReplicator((queenNotNull, comp), comp.FinalStage);
            if (!TryComp<ReplicatorComponent>(upgradedQueen, out var upgradedComp))
                return;

            if (upgradedQueen is not { } upgradedQueenNotNull || !TryComp<MindContainerComponent>(upgradedQueen, out var mindContainer) || mindContainer.Mind is not { } mind)
                return;

            upgradedComp.Actions.Add(!mindContainer.HasMind
                ? _actions.AddAction(upgradedQueenNotNull, upgradedComp.SpawnNewNestAction)
                : _actionContainer.AddAction(mind, upgradedComp.SpawnNewNestAction));

            // then add the Crown.
            EnsureComp<ReplicatorSignComponent>(upgradedQueenNotNull);
        }

        // finally, loop over our living replicators and set their pinpointers to target the queen, then downgrade them to level 1 and stun them.
        foreach (var replicator in livingReplicators)
        {
            // downgrade to level 1
            var upgraded = _replicator.UpgradeReplicator(replicator, replicator.Comp.FirstStage);
            if (upgraded is not { } upgradedNotNull)
                return;

            _stun.TryAddStunDuration(upgradedNotNull, TimeSpan.FromSeconds(3));

            if (!_inventory.TryGetSlotEntity(upgradedNotNull, "pocket1", out var pocket1) || !TryComp<PinpointerComponent>(pocket1, out var pinpointer))
                continue;

            // set the target to the queen
            _pinpointer.SetTarget(pocket1.Value, queen, pinpointer);
        }

        // turn off the ambient sound on the points storage entity.
        if (TryComp<AmbientSoundComponent>(ent.Comp.PointsStorage, out var ambientComp))
            _ambientSound.SetAmbience(ent.Comp.PointsStorage, false, ambientComp);
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent args)
    {
        List<Entity<ReplicatorNestPointsStorageComponent>> nests = [];

        // get all the nests that have existed this round in a list
        var query = AllEntityQuery<ReplicatorNestPointsStorageComponent>();
        while (query.MoveNext(out var uid, out var comp))
            nests.Add((uid, comp));

        if (nests.Count == 0)
            return;

        // linebreak
        args.AddLine("");

        var totalPoints = 0;
        var totalSpawned = 0;
        HashSet<int> levels = [];
        var locationsList = "";

        // generate a summary of locations, levels, points, and total spawned replicators across all nests
        var i = 0;
        foreach (var ent in nests)
        {
            i++;
            var pointsStorage = ent.Comp;
            var location = "Unknown";
            var mapCoords = _xform.ToMapCoordinates(Transform(ent).Coordinates);
            if (_navMap.TryGetNearestBeacon(mapCoords, out var beacon, out _) && beacon?.Comp.Text != null)
                location = beacon?.Comp.Text!;

            if (nests.Count == 1)
                locationsList = string.Concat(locationsList, "[color=#d70aa0]", location, "[/color].");
            else if (nests.Count == 2 && i == 1)
                locationsList = string.Concat(locationsList, "[color=#d70aa0]", location, " ");
            else if (i != nests.Count)
                locationsList = string.Concat(locationsList, "[color=#d70aa0]", location, "[/color], ");
            else
                locationsList = string.Concat(locationsList, $"[/color]and [color=#d70aa0]{location}[/color].");

            totalPoints += pointsStorage.TotalPoints / 10; // dividing by ten gives us a slightly more manageable number + keeps it consistent with pre-stackcount point calculation.

            totalSpawned += pointsStorage.TotalReplicators;

            levels.Add(pointsStorage.Level);
        }

        var highestLevel = levels.Max();

        // then push that summary.
        args.AddLine(Loc.GetString("replicator-nest-end-of-round", ("location", locationsList), ("level", highestLevel), ("points", totalPoints), ("replicators", totalSpawned)));
        args.AddLine("");
    }
}
