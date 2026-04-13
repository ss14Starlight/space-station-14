using System.Linq;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.NullSpace;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Random;

namespace Content.Server._Starlight.CosmicCult.Abilities;

public sealed class CosmicTransmuteSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicGlyphTransmuteComponent, TryActivateGlyphEvent>(OnTransmuteGlyph);
    }

    private void OnTransmuteGlyph(Entity<CosmicGlyphTransmuteComponent> uid, ref TryActivateGlyphEvent args)
    {
        foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
            if (HasComp<NullSpaceBlockerComponent>(entity))
            {
                _popup.PopupEntity(Loc.GetString("cosmicability-generic-fail"), uid, args.User);
                return;
            }

        var tgtpos = Transform(uid).Coordinates;
        var possibleTargets = GatherEntities(uid);
        if (possibleTargets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-glyph-conditions-not-met"), uid, args.User);
            args.Cancel();
            return;
        }
        if (possibleTargets.Count > 1)
        {
            _popup.PopupEntity(Loc.GetString("cult-glyph-too-many-targets"), uid, args.User);
            args.Cancel();
            return;
        }

        Spawn(_random.Pick(uid.Comp.Transmutations), tgtpos);
        QueueDel(possibleTargets.First());
    }


    /// <summary>
    ///     Gets all whitelisted entities near a glyph.
    /// </summary>
    private HashSet<EntityUid> GatherEntities(Entity<CosmicGlyphTransmuteComponent> ent)
    {
        var entities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.TransmuteRange, entities);
        entities.RemoveWhere(item => !_entityWhitelist.IsValid(ent.Comp.Whitelist, item));
        return entities;
    }
}
