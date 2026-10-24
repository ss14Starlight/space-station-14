using System.Linq;
using Content.Server._Starlight.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared._Starlight.Structures.Flip;
using Content.Shared._Starlight.Weapons.Cover.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.NPC.Systems;

/// <summary>
/// What hostile NPCs can see. Lockers, crates, vending machines and other cover don't hide anyone standing
/// behind them, and someone shutting themselves into a locker or crate stays known only to the NPCs that saw it:
/// those go for the storage, while NPCs that arrive later have no idea anybody is inside.
/// </summary>
public sealed partial class NPCHidingWitnessSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float MaxWitnessRange = 30f;

    private readonly HashSet<Entity<HTNComponent>> _witnesses = [];
    private readonly List<EntityUid> _forgotten = [];

    private SharedInteractionSystem.Ignored _seeThrough = default!;

    public override void Initialize()
    {
        base.Initialize();

        _seeThrough = IsSeeThrough;
    }

    private bool IsSeeThrough(EntityUid uid)
        => HasComp<EntityStorageComponent>(uid)
        || HasComp<FlippableStructureComponent>(uid)
        || HasComp<ProjectileCoverComponent>(uid);

    [SubscribeLocalEvent]
    private void OnInsertedIntoContainer(Entity<NpcFactionMemberComponent> hider, ref EntGotInsertedIntoContainerMessage args)
    {
        var storage = args.Container.Owner;
        if (!HasComp<EntityStorageComponent>(storage))
            return;

        _witnesses.Clear();
        _lookup.GetEntitiesInRange(Transform(storage).Coordinates, MaxWitnessRange, _witnesses);

        foreach (var npc in _witnesses)
        {
            if (npc.Owner == hider.Owner || _mobState.IsIncapacitated(npc))
                continue;

            if (!IsHostile(npc, hider))
                continue;

            var blackboard = npc.Comp.Blackboard;
            var vision = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(EntityManager), EntityManager);

            if (!_interaction.InRangeUnobstructed(npc.Owner, storage, vision, CollisionGroup.Opaque, _seeThrough))
                continue;

            EnsureComp<NPCHidingWitnessComponent>(npc).Hidden[hider] = storage;
        }
    }

    [SubscribeLocalEvent]
    private void OnRemovedFromContainer(Entity<NpcFactionMemberComponent> hider, ref EntGotRemovedFromContainerMessage args)
    {
        var storage = args.Container.Owner;
        if (!HasComp<EntityStorageComponent>(storage))
            return;

        var query = EntityQueryEnumerator<NPCHidingWitnessComponent>();
        while (query.MoveNext(out var npc, out var witness))
        {
            if (!witness.Hidden.TryGetValue(hider, out var known) || known != storage)
                continue;

            witness.Hidden.Remove(hider);
            if (witness.Hidden.Count == 0)
                RemCompDeferred<NPCHidingWitnessComponent>(npc);
        }
    }

    private bool IsHostile(EntityUid npc, EntityUid target)
    {
        if (TryComp<FactionExceptionComponent>(npc, out var exception))
        {
            if (_faction.IsIgnored((npc, exception), target))
                return false;

            if (_faction.GetHostiles((npc, exception)).Contains(target))
                return true;
        }

        return TryComp<NpcFactionMemberComponent>(npc, out var faction)
            && TryComp<NpcFactionMemberComponent>(target, out var targetFaction)
            && _faction.IsMemberOfAny((target, targetFaction), faction.HostileFactions)
            && !_faction.IsEntityFriendly((npc, faction), (target, targetFaction));
    }

    private bool TryGetHidingSpot(EntityUid target, out EntityUid storage)
    {
        storage = default;

        if (!_container.TryGetOuterContainer(target, Transform(target), out var container)
            || !HasComp<EntityStorageComponent>(container.Owner))
        {
            return false;
        }

        storage = container.Owner;
        return true;
    }

    /// <summary>
    /// Whether <paramref name="npc"/> saw <paramref name="hider"/> shut themselves into the storage they are in now.
    /// </summary>
    public bool KnowsHidingSpot(Entity<NPCHidingWitnessComponent?> npc, EntityUid hider, out EntityUid storage)
    {
        storage = default;

        return Resolve(npc.Owner, ref npc.Comp, false)
            && npc.Comp.Hidden.TryGetValue(hider, out var known)
            && TryGetHidingSpot(hider, out storage)
            && storage == known;
    }

    /// <summary>
    /// Whether the target is hidden in a locker or crate where this NPC didn't see them climb in.
    /// </summary>
    public bool IsHiddenFrom(EntityUid npc, EntityUid target)
        => TryGetHidingSpot(target, out _) && !KnowsHidingSpot(npc, target, out _);

    /// <summary>
    /// What the NPC should hit to get at the target: the storage it saw them hide in, otherwise the target itself.
    /// </summary>
    public EntityUid GetAttackTarget(EntityUid npc, EntityUid target)
        => KnowsHidingSpot(npc, target, out var storage) ? storage : target;

    /// <summary>
    /// Line of sight check for NPCs that looks over lockers, crates and cover, and only finds someone hidden
    /// in a storage if the NPC saw them get in.
    /// </summary>
    public bool CanSee(EntityUid npc, EntityUid target, float range, CollisionGroup collisionGroup)
    {
        if (TryGetHidingSpot(target, out _))
        {
            if (!KnowsHidingSpot(npc, target, out var storage))
                return false;

            target = storage;
        }

        return _interaction.InRangeUnobstructed(npc, target, range, collisionGroup, _seeThrough);
    }

    /// <summary>
    /// Adds the hostiles this NPC saw hiding nearby to its target candidates. Regular lookups skip anything inside a container.
    /// </summary>
    public void AddWitnessedHiders(Entity<NPCHidingWitnessComponent?> npc, float range, HashSet<EntityUid> entities)
    {
        if (!Resolve(npc.Owner, ref npc.Comp, false))
            return;

        var npcPos = _transform.GetMapCoordinates(npc.Owner);
        _forgotten.Clear();

        foreach (var (hider, storage) in npc.Comp.Hidden)
        {
            if (TerminatingOrDeleted(hider)
                || TerminatingOrDeleted(storage)
                || !TryGetHidingSpot(hider, out var current)
                || current != storage)
            {
                _forgotten.Add(hider);
                continue;
            }

            var storagePos = _transform.GetMapCoordinates(storage);
            if (storagePos.MapId != npcPos.MapId || (storagePos.Position - npcPos.Position).Length() > range)
                continue;

            if (IsHostile(npc, hider))
                entities.Add(hider);
        }

        foreach (var hider in _forgotten)
            npc.Comp.Hidden.Remove(hider);

        if (npc.Comp.Hidden.Count == 0)
            RemCompDeferred<NPCHidingWitnessComponent>(npc);
    }
}
