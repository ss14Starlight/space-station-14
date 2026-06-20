using System.Linq;
using Content.Server.Flash;
using Content.Server.Light.EntitySystems;
using Content.Server.Stunnable;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Effects;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Shared.Light.Components;
using Content.Server.Bible.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared._Starlight.NullSpace;
using Content.Server.Popups;
using Content.Shared._Starlight.NullSpace.Components;

namespace Content.Server._Starlight.CosmicCult.Abilities;

public sealed partial class CosmicGlareSystem : EntitySystem
{
    [Dependency] private CosmicCultSystem _cult = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FlashSystem _flash = default!;
    [Dependency] private PoweredLightSystem _poweredLight = default!;
    [Dependency] private StunSystem _stun = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private SharedCosmicCultSystem _cosmicCult = default!;
    [Dependency] private SharedInteractionSystem _interact = default!;
    [Dependency] private PopupSystem _popup = default!;

    private readonly HashSet<Entity<PoweredLightComponent>> _lights = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicCultComponent, EventCosmicGlare>(OnCosmicGlare);
    }

    private void OnCosmicGlare(Entity<CosmicCultComponent> uid, ref EventCosmicGlare args)
    {
        foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
            if (HasComp<NullSpaceBlockerComponent>(entity))
            {
                _popup.PopupEntity(Loc.GetString("cosmicability-generic-fail"), uid, uid);
                return;
            }

        _audio.PlayPvs(uid.Comp.GlareSFX, uid);
        Spawn(uid.Comp.GlareVFX, Transform(uid).Coordinates);
        _cult.MalignEcho(uid);
        args.Handled = true;

        _lights.Clear();
        _lookup.GetEntitiesInRange(Transform(uid).Coordinates, uid.Comp.CosmicGlareRange, _lights);

        foreach (var entity in _lights)
            _poweredLight.TryDestroyBulb(entity);

        var targetFilter = Filter.Pvs(uid).RemoveWhere(player =>
        {
            if (player.AttachedEntity == null)
                return true;

            var ent = player.AttachedEntity.Value;
            if (!HasComp<MobStateComponent>(ent) || _cosmicCult.EntityIsCultist(ent) || HasComp<BibleUserComponent>(ent) || HasComp<MindShieldComponent>(ent))
                return true;

            return !_interact.InRangeUnobstructed((uid, Transform(uid)), (ent, Transform(ent)), range: 0, collisionMask: CollisionGroup.Impassable);
        });

        var targets = new HashSet<NetEntity>(targetFilter.RemovePlayerByAttachedEntity(uid).Recipients.Select(ply => GetNetEntity(ply.AttachedEntity!.Value)));
        foreach (var target in targets)
        {
            var targetEnt = GetEntity(target);

            _flash.Flash(targetEnt, uid, args.Action, uid.Comp.CosmicGlareDuration, uid.Comp.CosmicGlarePenalty, false, false, uid.Comp.CosmicGlareStun);

            if (HasComp<BorgChassisComponent>(targetEnt))
            {
                _stun.TryAddParalyzeDuration(targetEnt, uid.Comp.CosmicGlareDuration / 2);
            }

            _color.RaiseEffect(Color.CadetBlue, new List<EntityUid>() { targetEnt }, Filter.Pvs(targetEnt, entityManager: EntityManager));
        }
    }
}
