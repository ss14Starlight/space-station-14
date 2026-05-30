using System.Numerics;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Behaviors.Pack;

/// <summary>
/// Handles pack recruitment logic and determining whether the pack has reached critical mass.
/// </summary>
public abstract class SharedQuoremCheckSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected Dictionary<int, HashSet<EntityUid>> _packGroups = new Dictionary<int, HashSet<EntityUid>>();
    private int _nextId;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<QuoremCheckComponent, MobStateChangedEvent>(OnPackmateDeath);
        SubscribeLocalEvent<QuoremCheckComponent, ComponentInit>(OnComponentInit);
    }

    /// <summary>
    /// Initializes component by providing individuals with pack ids.
    /// </summary>
    private void OnComponentInit(Entity<QuoremCheckComponent> ent, ref ComponentInit args)
    {
       ent.Comp.PackId = _nextId;
       _packGroups.Add(_nextId, new HashSet<EntityUid>(){ent});
       _nextId ++;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<QuoremCheckComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.NextRecruitAttempt)
                continue;

            component.NextRecruitAttempt += _random.Next(component.MinRecruitAttemptInterval, component.MaxRecruitAttemptInterval);

            TryJoinPacksNearby(uid, component);
        }
    }

    /// <summary>
    /// Checks nearby entities for joinable packs, and attempts to join them.
    /// </summary>
    private void TryJoinPacksNearby(EntityUid uid, QuoremCheckComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var xform = Transform(uid);

        var partners = new HashSet<Entity<QuoremCheckComponent>>();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, component.RecruitRadius, partners);

        foreach (var comp in partners)
        {
            var partner = comp.Owner;
            TryJoinPack(uid, partner, component);
        }
    }

    /// <summary>
    /// Try to join the pack of the target.
    /// </summary>
    private void TryJoinPack(EntityUid uid, EntityUid targetUid, QuoremCheckComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (uid == targetUid)
            return;

        QuoremCheckComponent? targetComponent = null;
        if (!Resolve(targetUid, ref targetComponent))
            return;

        if (component.PackTag != targetComponent.PackTag)
            return;

        if (component.PackId <= targetComponent.PackId)
            return;

        if (!_packGroups.ContainsKey(component.PackId) || !_packGroups.ContainsKey(targetComponent.PackId))
            return;

        if (_mobState.IsDead(uid) || _mobState.IsDead(targetUid))
            return;

        // Remove this pack from the list of packs
        _packGroups.Remove(component.PackId, out var pack);

        pack ??= new HashSet<EntityUid>();

        // Combine packs
        _packGroups[targetComponent.PackId].UnionWith(pack);

        // How many members are in the pack
        var packSize = _packGroups[targetComponent.PackId].Count;

        // Update other pack members
        foreach (var packMemberUid in _packGroups[targetComponent.PackId])
        {
            UpdateMembership(packMemberUid, targetComponent.PackId, packSize);
        }
    }

    /// <summary>
    /// Update an individual's membership when they enter a new pack
    /// </summary>
    private void UpdateMembership(EntityUid uid, int newPackId, int packSize)
    {
        if(!TryComp(uid, out QuoremCheckComponent? comp))
            return;
        comp.PackId = newPackId;
        UpdateHostile((uid, comp), packSize);
    }

    /// <summary>
    /// Remove a pack member on death
    /// </summary>
    private void OnPackmateDeath(Entity<QuoremCheckComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!_packGroups.TryGetValue(ent.Comp.PackId, out var pack))
            return;
        pack.Remove(ent);
        ent.Comp.PackId = _nextId;
        _nextId++;

        // If there are now 0 members in the pack remove it
        if(pack.Count == 0)
            _packGroups.Remove(ent.Comp.PackId);
    }

    /// <summary>
    /// Change faction to a hostile faction
    /// </summary>
    private void MakeHostile(Entity<QuoremCheckComponent> ent)
    {
        ent.Comp.IsHostile = true;
        _npcFactionSystem.ClearFactions((ent, null));
        _npcFactionSystem.AddFaction((ent, null), ent.Comp.QuoremFaction);
        var coords = new EntityCoordinates(ent.Owner, new Vector2(0, 1));
        SpawnAttachedTo(ent.Comp.QuoremEffect, coords);
        _audio.PlayPvs(ent.Comp.QuoremSound, ent.Owner);
    }

    /// <summary>
    /// Check to see if the Quorem has been reached. If so become hostile
    /// </summary>
    public void UpdateHostile(Entity<QuoremCheckComponent> ent, int packSize)
    {
        if (packSize >= ent.Comp.QuoremThreshold && !ent.Comp.IsHostile)
        {
            MakeHostile(ent);
        }
    }
}
