using Content.Shared._Starlight.Weapons.Cover.Components;
using Content.Shared._Starlight.Weapons.Cover.Systems;
using Content.Shared._Starlight.Traits.Unlucky;
using Content.Shared.Climbing.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Structures.Flip;

public sealed partial class SharedStructureFlipSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedProjectileCoverSystem _cover = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly SoundSpecifier _flipSound =
        new SoundCollectionSpecifier("MetalThud") { Params = AudioParams.Default.WithVariation(0.125f) };

    [SubscribeLocalEvent]
    private void OnGetFlipVerbs(Entity<FlippableStructureComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        if (HasComp<FlippedStructureComponent>(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryStartFlip(ent.Owner, user, ent.Comp.Delay),
            Text = Loc.GetString("structure-flip-verb-flip"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png")),
            DoContactInteraction = true,
            Priority = -1,
        });
    }

    [SubscribeLocalEvent]
    private void OnGetUnflipVerbs(Entity<FlippedStructureComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        var delay = ent.Comp.UprightPrototype == null && TryComp<FlippableStructureComponent>(ent, out var flippable)
            ? flippable.UnflipDelay
            : ent.Comp.Delay;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryStartFlip(ent.Owner, user, delay),
            Text = Loc.GetString("structure-flip-verb-unflip"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rotate_ccw.svg.192dpi.png")),
            DoContactInteraction = true,
            Priority = -1,
        });
    }

    private void TryStartFlip(EntityUid target, EntityUid user, TimeSpan delay)
        => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, new StructureFlipDoAfterEvent(), target, target: target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        });


    [SubscribeLocalEvent]
    private void OnFlipDoAfter(Entity<FlippableStructureComponent> ent, ref StructureFlipDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || HasComp<FlippedStructureComponent>(ent))
            return;

        args.Handled = true;

        if (ent.Comp.FlippedPrototype is { } flipped)
            Swap(ent.Owner, flipped, args.User, faceUser: true);
        else
            FlipInPlace(ent, args.User, fallTowards: false, predictedBy: args.User);
    }

    [SubscribeLocalEvent]
    private void OnUnflipDoAfter(Entity<FlippedStructureComponent> ent, ref StructureFlipDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.UprightPrototype is { } upright)
            Swap(ent.Owner, upright, args.User, faceUser: false);
        else
            UnflipInPlace(ent);
    }

    private void Swap(EntityUid target, EntProtoId prototype, EntityUid user, bool faceUser)
    {
        var xform = Transform(target);
        var coordinates = xform.Coordinates;
        var wasAnchored = xform.Anchored;

        DamageSpecifier? damage = null;
        if (TryComp<DamageableComponent>(target, out var oldDamage))
            damage = new DamageSpecifier(oldDamage.Damage);

        var rotation = faceUser
            ? GetRotationTowards(target, user, xform)
            : Angle.Zero;

        _audio.PlayPredicted(_flipSound, target, user);

        PredictedDel(target);

        var replacement = PredictedSpawnAtPosition(prototype, coordinates);
        _transform.SetLocalRotation(replacement, rotation);

        var transform = Transform(replacement);
        if (!transform.Anchored && wasAnchored)
            _transform.AnchorEntity(replacement, transform);
        else if (transform.Anchored && !wasAnchored)
            _transform.Unanchor(replacement);

        if (damage != null && HasComp<DamageableComponent>(replacement))
            _damageable.SetDamage(replacement, damage);
    }

    private Angle GetRotationTowards(EntityUid target, EntityUid user, TransformComponent xform)
    {
        var targetPos = _transform.GetMapCoordinates(target);
        var userPos = _transform.GetMapCoordinates(user);

        if (targetPos.MapId != userPos.MapId)
            return Angle.Zero;

        var offset = userPos.Position - targetPos.Position;

        if (offset.LengthSquared() < 0.01f)
            return Angle.Zero;

        var worldAngle = offset.ToWorldAngle();
        var parentRotation = _transform.GetWorldRotation(xform) - xform.LocalRotation;

        return (worldAngle - parentRotation).GetCardinalDir().ToAngle();
    }

    private void FlipInPlace(Entity<FlippableStructureComponent> ent, EntityUid pivot, bool fallTowards, EntityUid? predictedBy)
    {
        var flipped = EnsureComp<FlippedStructureComponent>(ent);
        flipped.UprightPrototype = null;
        Dirty(ent, flipped);

        if (TryComp<FixturesComponent>(ent, out var fixtures))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                if (!fixture.Hard)
                    continue;

                flipped.SavedFixtures[id] = (fixture.CollisionLayer, fixture.CollisionMask);
                _physics.SetCollisionLayer(ent, id, fixture, ent.Comp.FlippedLayer, fixtures);
                _physics.SetCollisionMask(ent, id, fixture, ent.Comp.FlippedMask, fixtures);
            }
        }

        if (!HasComp<ClimbableComponent>(ent))
        {
            AddComp<ClimbableComponent>(ent);
            flipped.AddedClimbable = true;
        }

        if (!HasComp<ProjectileCoverComponent>(ent))
        {
            var cover = AddComp<ProjectileCoverComponent>(ent);
            _cover.SetBlockChance((ent, cover), ent.Comp.CoverChance);
            flipped.AddedCover = true;
        }

        SharedApcPowerReceiverComponent? receiver = null;
        if (ent.Comp.DisablePower && _power.ResolveApc(ent, ref receiver))
        {
            flipped.WasPowerDisabled = receiver.PowerDisabled;
            _power.SetPowerDisabled(ent, true, receiver);
        }

        var tilt = GetTiltAwayFrom(ent, pivot);
        _appearance.SetData(ent, FlippedStructureVisuals.Tilt, fallTowards ? -tilt : tilt);

        if (predictedBy != null)
            _audio.PlayPredicted(_flipSound, ent, predictedBy);
        else
            _audio.PlayPvs(_flipSound, ent);
    }

    private void UnflipInPlace(Entity<FlippedStructureComponent> ent)
    {
        if (TryComp<FixturesComponent>(ent, out var fixtures))
        {
            foreach (var (id, (layer, mask)) in ent.Comp.SavedFixtures)
            {
                if (!fixtures.Fixtures.TryGetValue(id, out var fixture))
                    continue;

                _physics.SetCollisionLayer(ent, id, fixture, layer, fixtures);
                _physics.SetCollisionMask(ent, id, fixture, mask, fixtures);
            }
        }

        if (ent.Comp.AddedClimbable)
            RemComp<ClimbableComponent>(ent);

        if (ent.Comp.AddedCover)
            RemComp<ProjectileCoverComponent>(ent);

        if (ent.Comp.WasPowerDisabled is { } wasDisabled)
            _power.SetPowerDisabled(ent, wasDisabled);

        _appearance.RemoveData(ent, FlippedStructureVisuals.Tilt);
        RemComp<FlippedStructureComponent>(ent);
    }

    private Angle GetTiltAwayFrom(EntityUid target, EntityUid user)
    {
        var targetPos = _transform.GetWorldPosition(target);
        var userPos = _transform.GetWorldPosition(user);

        return userPos.X > targetPos.X
            ? Angle.FromDegrees(90)
            : Angle.FromDegrees(-90);
    }

    [SubscribeLocalEvent]
    private void OnStartCollide(Entity<FlippableStructureComponent> ent, ref StartCollideEvent args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.CrushDamage == null
            || ent.Comp.FlippedPrototype != null
            || !args.OurFixture.Hard
            || !args.OtherFixture.Hard
            || HasComp<FlippedStructureComponent>(ent))
        {
            return;
        }

        var victim = args.OtherEntity;

        if (!HasComp<MobStateComponent>(victim))
            return;

        if (TryComp<ClimbingComponent>(victim, out var climbing) && climbing.IsClimbing)
            return;

        var chance = 0f;

        if (HasComp<KnockedDownComponent>(victim) && args.OtherBody.LinearVelocity.Length() >= ent.Comp.SlipMinSpeed)
            chance = ent.Comp.SlipTopplingChance;

        if (TryComp<UnluckyComponent>(victim, out var unlucky))
            chance = MathF.Max(chance, unlucky.TopplingChance);

        if (chance <= 0f || !_random.Prob(chance))
            return;

        ToppleOnto(ent, victim);
    }

    public void ToppleOnto(Entity<FlippableStructureComponent> ent, EntityUid victim)
    {
        if (ent.Comp.CrushDamage is not { } damage || HasComp<FlippedStructureComponent>(ent))
            return;

        FlipInPlace(ent, victim, fallTowards: true, predictedBy: null);

        _damageable.TryChangeDamage(victim, damage, origin: ent);
        _stun.TryKnockdown(victim, ent.Comp.CrushKnockdown, refresh: true);
        _audio.PlayPvs(ent.Comp.CrushSound, victim);

        _popup.PopupEntity(Loc.GetString("structure-flip-topple-victim", ("structure", ent.Owner)),
            victim, victim, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("structure-flip-topple-others", ("structure", ent.Owner), ("victim", victim)),
            victim, Filter.PvsExcept(victim), true, PopupType.MediumCaution);
    }
}

[Serializable, NetSerializable]
public sealed partial class StructureFlipDoAfterEvent : SimpleDoAfterEvent;
