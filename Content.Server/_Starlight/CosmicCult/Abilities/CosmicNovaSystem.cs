using System.Numerics;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared.Effects;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Server.Popups;
using Content.Shared.Mindshield.Components;
using Content.Shared._Starlight.NullSpace;
using Content.Server.Bible.Components;
using Content.Shared._Starlight.NullSpace.Components;

namespace Content.Server._Starlight.CosmicCult.Abilities;

public sealed partial class CosmicNovaSystem : EntitySystem
{
    [Dependency] private CosmicCultSystem _cult = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private SharedCosmicCultSystem _cosmicCult = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PopupSystem _popup = default!;

    private static readonly EntProtoId _projectile = "ProjectileCosmicNova";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicCultComponent, EventCosmicNova>(OnCosmicNova);
        SubscribeLocalEvent<CosmicAstralNovaComponent, StartCollideEvent>(OnNovaCollide);
    }

    /// <summary>
    /// This is the basic spell projectile code but updated to use non-obsolete functions, all so i can change the default projectile speed. Fuck.
    /// </summary>
    private void OnCosmicNova(Entity<CosmicCultComponent> uid, ref EventCosmicNova args)
    {
        foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
            if (HasComp<NullSpaceBlockerComponent>(entity))
            {
                _popup.PopupEntity(Loc.GetString("cosmicability-generic-fail"), uid, uid);
                return;
            }

        var startPos = _transform.GetMapCoordinates(args.Performer);
        var targetPos = _transform.ToMapCoordinates(args.Target);
        var userVelocity = _physics.GetMapLinearVelocity(args.Performer);

        var delta = targetPos.Position - startPos.Position;
        if (delta.EqualsApprox(Vector2.Zero))
            delta = new(.01f, 0);

        args.Handled = true;
        var ent = Spawn(_projectile, startPos);
        _gun.ShootProjectile(ent, delta, userVelocity, args.Performer, args.Performer, 5f);
        _audio.PlayPvs(uid.Comp.NovaCastSFX, uid, AudioParams.Default.WithVariation(0.1f));
        _cult.MalignEcho(uid);
    }

    private void OnNovaCollide(Entity<CosmicAstralNovaComponent> uid, ref StartCollideEvent args)
    {
        if (_cosmicCult.EntityIsCultist(args.OtherEntity) || !HasComp<MobStateComponent>(args.OtherEntity) || HasComp<BibleUserComponent>(args.OtherEntity) || HasComp<MindShieldComponent>(args.OtherEntity))
            return;
        if (uid.Comp.DoStun)
            _stun.TryAddParalyzeDuration(args.OtherEntity, TimeSpan.FromSeconds(2f));
        _damageable.TryChangeDamage(args.OtherEntity, uid.Comp.CosmicNovaDamage); // This'll probably trigger two or three times because of how collision works. I'm not being lazy here, it's a feature (kinda /s)
        _color.RaiseEffect(Color.Red, [args.OtherEntity], Filter.Pvs(args.OtherEntity, entityManager: EntityManager));
    }
}
