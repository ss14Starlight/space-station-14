//Copyright © 2025 .cerol (Discord), Licensed under MIT License.
//Changes after https://github.com/ss14Starlight/space-station-14/pull/2054/commits/e18dafedad110b20cdc17d054fe35413a1831f59 licensed under Starlight License.

using Content.Shared.Body.Events;
using Content.Shared.Damage;
using Content.Shared.Gibbing.Events;
using Content.Shared.Throwing;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Network;
using Content.Shared.Damage.Systems;

namespace Content.Server._Starlight.PinataSystem;

public sealed class PinataSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly EntityTableSystem _entityTable =  default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PinataComponent, DamageModifyEvent>(OnHit);
        SubscribeLocalEvent<PinataComponent, BeingGibbedEvent>(OnGib);
        SubscribeLocalEvent<PinataComponent, EntityGibbedEvent>(OnGibAlt);
    }

    //This is from most explicit gib effects
    private void OnGibAlt(Entity<PinataComponent> ent, ref EntityGibbedEvent args) => RemoveGibbedParts(ent, args.DroppedEntities);

    //This is from taking too much damage and gibbing.
    private void OnGib(Entity<PinataComponent> ent, ref BeingGibbedEvent args) => RemoveGibbedParts(ent, args.GibbedParts);

    private void RemoveGibbedParts(Entity<PinataComponent> ent, ICollection<EntityUid> guts)
    {
        foreach (var organ in guts)
            QueueDel(organ);
        
        guts.Clear();
        
        if (ent.Comp.GibTable == null)
            return;

        SpawnItem(ent, ent.Comp.GibTable);
    }

    private void OnHit(Entity<PinataComponent> ent, ref DamageModifyEvent args)
    {
        var damPerGroup = args.Damage.GetDamagePerGroup(_proto);
        if (!damPerGroup.TryGetValue("Brute", out var brute) || brute <= 5 || ent.Comp.HitTable == null) //Has to be a decent hit
            return;
            
        SpawnItem(ent, ent.Comp.HitTable);
    }

    /// <summary>
    /// Custom method which spawns entities in random range.
    /// </summary>
    private void SpawnItem(Entity<PinataComponent> entity, EntityTableSelector entitiesToSpawn)
    {
        if (_net.IsClient) // No prediction for entity table.
          return;

        var spawns = _entityTable.GetSpawns(entitiesToSpawn);
        var coords = Transform(entity).Coordinates;
        foreach (var spawn in spawns)
        {
            var spawnedEntity = Spawn(spawn, coords);
            _throwing.TryThrow(spawnedEntity , _random.NextVector2(), baseThrowSpeed: 5f);
        }

    }
}
