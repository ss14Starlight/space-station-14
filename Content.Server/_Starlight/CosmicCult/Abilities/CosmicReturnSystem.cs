using Content.Server._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult.Components.Examine;
using Content.Shared.Damage;
using Content.Shared.Mind;
using Content.Shared.Stunnable;
using Content.Shared.Damage.Systems;
using Content.Shared._Starlight.Polymorph.Components;

namespace Content.Server._Starlight.CosmicCult.Abilities;

public sealed class CosmicReturnSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicAstralBodyComponent, EventCosmicReturn>(OnCosmicReturn);
        SubscribeLocalEvent<CosmicGlyphAstralProjectionComponent, TryActivateGlyphEvent>(OnAstralProjectionGlyph);
    }

    private void OnAstralProjectionGlyph(Entity<CosmicGlyphAstralProjectionComponent> uid, ref TryActivateGlyphEvent args)
    {
        _damageable.TryChangeDamage(args.User, uid.Comp.ProjectionDamage, true);
        var projectionEnt = Spawn(uid.Comp.SpawnProjection, Transform(uid).Coordinates);
        if (_mind.TryGetMind(args.User, out var mindId, out var _))
            _mind.TransferTo(mindId, projectionEnt);
        EnsureComp<CosmicBlankComponent>(args.User);
        EnsureComp<UncryoableComponent>(args.User); //Starlight: autocryo fix
        EnsureComp<CosmicAstralBodyComponent>(projectionEnt, out var astralComp);
        var mind = Comp<MindComponent>(mindId);
        mind.PreventGhosting = true;
        astralComp.OriginalBody = args.User;
        _stun.TryKnockdown(args.User, TimeSpan.FromSeconds(2), true);
    }

    /// <summary>
    ///     This action is exclusive to the Glyph-created Astral Projection, and allows the user to return to their original body.
    /// </summary>
    private void OnCosmicReturn(Entity<CosmicAstralBodyComponent> uid, ref EventCosmicReturn args)
    {
        if (_mind.TryGetMind(args.Performer, out var mindId, out var _))
            _mind.TransferTo(mindId, uid.Comp.OriginalBody);
        var mind = Comp<MindComponent>(mindId);
        mind.PreventGhosting = false;
        QueueDel(uid);
        RemComp<CosmicBlankComponent>(uid.Comp.OriginalBody);
        RemComp<UncryoableComponent>(uid.Comp.OriginalBody); //Starlight: autocryo fix
        RemComp<CosmicCultExamineComponent>(uid.Comp.OriginalBody);
    }
}
