using System.Diagnostics.CodeAnalysis;
using Content.Server.NPC.Pathfinding;
using Content.Shared._Starlight.Xenobiology;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Xenobiology;

/*
 * Slimes are a hive mind! Why? Because it's easier to program, there will only be one so we don't have to worry about a big update loop, and its hilarious.
 * This is going to be primarily event based and work with HTN, with the following design philosophy:
 * Slimes will each have Moods that determine their overall behavior. Moods inform the HTN as to which branch to take when deciding slime behavior.
 * For instance, calm slimes will slowly wander around and eat anything nearby tagged with Monkey, while hungry slimes will eat anything biological.
 * The slime hivemind holds memories. Memories are globally known dictionaries holding information relevant for slimes. Memories are shared by all slimes.
 * The most basic memory is opinion. Each entry is an EntityUid paired with a fixedpoint ranging from -100 to 100, with -100 being hatred and 100 being love.
 * Slimes, all slimes, will take orders from loved individuals and will KOS any hated individuals.
 * Opinion doesn't decay to any resting state. You make friends with slimes by feeding them and make enemies with slimes by attacking them or anyone they love.
 * Don't be surprised if shooting the Xenobiologist causes The Slime Swarm to brutalize you.
 * Memories dictate moods and their transitions. For instance, a slime will go from Calm to Hostile if there is an entity nearby with -50 opinion.
 * And more interestingly, a slime will enter go from Hungry to Search if commanded by someone with 100 opinion.
 * Slimes have poor situational awareness and will only switch their moods on an UpdateMood event, usually raised by HTN.
 * Because updating mood every tick sounds like a performance nightmare.
 */

public enum SlimeMood
{
    Calm,
    Desperate,
}

public sealed class SlimeBrainSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;
    
    /// <summary>
    /// The set of food targets slimes can safely eat.
    /// </summary>
    private HashSet<EntityUid> TargetFood = new();

    /// <summary>
    /// The locations marked by slimes indicating there may be food nearby.
    /// Specifically, if a slime eats a monkey at a spot, they will mark it as a known food location.
    /// If a slime arrived to the spot and doesn't find any food to eat, they will un-mark it.
    /// </summary>
    private HashSet<EntityCoordinates> KnownFoodLocations = new();

    /// <summary>
    /// How far to look for food at each slime.
    /// </summary>
    public readonly float FoodSearchRange = 5F;
    
    /// <summary>
    /// The amount of damage below which the slime brain will consider the target to be edible.
    /// </summary>
    public readonly FixedPoint2 TargetDamageThreshold = 100;
    
    /// <summary>
    /// If not null, will only allow slimes to eat entities with the specified damage container.
    /// If null, will make slimes try to eat everything.
    /// </summary>
    public readonly ProtoId<DamageContainerPrototype>? OnlyTarget = "Biological";

    public bool IsEdibleBySlimeTest(EntityUid entity)
    {
        // Don't cannibalize other slimes
        if (_entManager.HasComponent<SlimeComponent>(entity)) return false;

        if (!_entManager.TryGetComponent<DamageableComponent>(entity, out var damage)) return false;
        
        // Don't target entities that aren't mobs
        if (!_entManager.HasComponent<MobStateComponent>(entity)) return false;

        // Don't target entities in the wrong damage group
        if (OnlyTarget.HasValue)
            if (!(damage.DamageContainerID.HasValue && damage.DamageContainerID.Value == OnlyTarget.Value))
                return false;

        // Only target entities that are not damaged enough
        if (!(damage.TotalDamage < TargetDamageThreshold)) return false;

        return true;
    }

    /// <summary>
    /// Attempts to add a food target to the slime brain. Targets are only added if they pass the edible slime test (see <see cref="IsEdibleBySlimeTest"/>).
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>Returns true if successful, returns false if the target food fails the test and thus cannot be added.</returns>
    public bool TryAddTargetFood(EntityUid entity)
    {
        if (IsEdibleBySlimeTest(entity))
        {
            TargetFood.Add(entity);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Grabs the set of valid food targets that are known to the brain
    /// </summary>
    /// <returns>The set of valid food targets. May be empty.</returns>
    public HashSet<EntityUid> AcquireTargetFoods()
    {
        HashSet<EntityUid> targetsToReturn = new();
        HashSet<EntityUid> targetsToDelete = new();
        foreach (var possibleTarget in TargetFood)
        {
            if (IsEdibleBySlimeTest(possibleTarget))
            {
                targetsToReturn.Add(possibleTarget);
            }
            else
            {
                targetsToDelete.Add(possibleTarget);
            }
        }
        foreach (var delete in targetsToDelete)
        {
            TargetFood.Remove(delete);
        }
        return targetsToReturn;
    }

    /// <summary>
    /// Adds a given coordinate to the known feeding spots set.
    /// </summary>
    /// <param name="coordinates">The feeding spot location to add.</param>
    public void AddFeedingSpot(EntityCoordinates coordinates) => KnownFoodLocations.Add(coordinates);

    /// <summary>
    /// Retrieves the set of feeding spots known to the slime brain.
    /// </summary>
    /// <returns>The set of feeding spots.</returns>
    /// YES I KNOW THIS IS A CLONE OPERATION GET OFF MY BACK
    public HashSet<EntityCoordinates> AcquireFeedingSpots()
    {
        HashSet<EntityCoordinates> coordsToReturn = new();
        foreach (var coord in KnownFoodLocations)
        {
            coordsToReturn.Add(coord);
        }

        return coordsToReturn;
    }

    /// <summary>
    /// Called by slimes if they successfully eat food.
    /// </summary>
    /// <param name="entity">The slime entity.</param>
    public void SlimeSuccessfulEat(EntityUid entity)
    {
        KnownFoodLocations.Add(_entManager.GetComponent<TransformComponent>(entity).Coordinates);
    }

    /// <summary>
    /// Called by slimes if they couldn't find anything nearby to eat.
    /// </summary>
    /// <param name="entity">The slime entity.</param>
    public void SlimeUnsuccessfulFoodFind(EntityUid entity)
    {
        KnownFoodLocations.Remove(_entManager.GetComponent<TransformComponent>(entity).Coordinates);
    }
}