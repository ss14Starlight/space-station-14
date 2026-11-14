using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Starlight.Spider.Events;
using Content.Shared.Spider;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;

// ReSharper disable once CheckNamespace
namespace Content.Server.Spider;

public sealed partial class SpiderSystem
{

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    [SubscribeLocalEvent]
    public void OnMeleeHit(EntityUid uid, SpiderComponent component, ref MeleeHitEvent args)
    {
        if (component.CantBreakWeb && args.HitEntities.Any(HasComp<SpiderWebObjectComponent>))
            args.BonusDamage = -args.BaseDamage;
    }

    private void SLSpawnWeb(Entity<SpiderComponent> ent, EntityCoordinates coords, ref bool  shouldRunEvent, ref bool result)
    {
        if (!ent.Comp.OneWebSpawn)
        {
            // Spawn web in other directions
            for (var i = 0; i < 4; i++)
            {
                var direction = (DirectionFlag)(1 << i);
                var outerSpawnCoordinates = coords.Offset(direction.AsDir().ToVec());

                if (IsTileBlockedByWeb(outerSpawnCoordinates, out var web1))
                {
                    if (ent.Comp.ReplacementAllowed && web1.Value.Comp.Replaceable)
                    {
                        QueueDel(web1.Value);
                        SpawnAtPosition(ent.Comp.WebPrototype, outerSpawnCoordinates);
                        shouldRunEvent = false;
                        result = true;
                    }
                }
                else
                {
                    SpawnAtPosition(ent.Comp.WebPrototype, outerSpawnCoordinates);
                    result = true;
                }
            }
        }

        if (result && shouldRunEvent)
        {
            var ev = new SpiderWebSpawnedEvent();
            RaiseLocalEvent(ent.Owner, ev);
        }
    }

    private bool IsTileBlockedByWeb(EntityCoordinates coords, [NotNullWhen(true)] out Entity<SpiderWebObjectComponent>? web)
    {
        web = null;
        _webs.Clear();
        _webs.UnionWith(_lookup.GetEntitiesIntersecting(coords));
        foreach (var entity in _webs)
        {
            if (TryComp<SpiderWebObjectComponent>(entity, out var webObj))
            {
                web = (entity, webObj);
                return true;
            }
        }
        return false;
    }
}
