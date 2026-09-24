using System.Numerics;
using Content.Shared._Starlight.Weapons.Cover.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Starlight.Weapons.Cover.Systems;

public sealed partial class SharedProjectileCoverSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    private const float ShelterSearchRange = 2f;

    private const float ShelterLineTolerance = 0.75f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileCoverComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<TargetedProjectileComponent, PreventCollideEvent>(OnTargetedPreventCollide);
    }

    private void OnPreventCollide(Entity<ProjectileCoverComponent> cover, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        var other = args.OtherEntity;

        if (!TryComp<ProjectileComponent>(other, out var projectile))
            return;

        if ((args.OtherFixture.CollisionMask & (int)CollisionGroup.BulletImpassable) == 0)
            return;

        var aimedAt = CompOrNull<TargetedProjectileComponent>(other)?.Target;
        var direction = Direction(args.OtherBody.LinearVelocity);

        if (!IsShotStopped(cover, other, projectile.Shooter, aimedAt: aimedAt, shotDirection: direction))
            args.Cancelled = true;
    }

    private void OnTargetedPreventCollide(Entity<TargetedProjectileComponent> shot, ref PreventCollideEvent args)
    {
        if (args.Cancelled || args.OtherEntity != shot.Comp.Target)
            return;

        if (!HasComp<ProjectileComponent>(shot)
            || (args.OurFixture.CollisionMask & (int)CollisionGroup.BulletImpassable) == 0)
        {
            return;
        }

        if (Direction(args.OurBody.LinearVelocity) is { } direction && IsShelteredFromShot(args.OtherEntity, direction))
            args.Cancelled = true;
    }

    public void SetBlockChance(Entity<ProjectileCoverComponent> cover, float chance)
    {
        cover.Comp.BlockChance = chance;
        Dirty(cover);
    }

    public bool IsShotStopped(
        Entity<ProjectileCoverComponent> cover,
        EntityUid shot,
        EntityUid? shooter,
        float? distance = null,
        EntityUid? aimedAt = null,
        Vector2? shotDirection = null)
    {
        var comp = cover.Comp;

        if (comp.BlockChance <= 0f)
            return false;

        if (_whitelist.IsWhitelistFail(comp.Whitelist, shot) || _whitelist.IsWhitelistPass(comp.Blacklist, shot))
            return false;

        if (aimedAt == cover.Owner)
            return true;

        if (aimedAt is { } target && shotDirection is { } direction && IsSheltering(cover, target, direction))
            return true;

        if (IsPointBlank(cover, shooter, distance))
            return false;

        if (comp.BlockChance >= 1f)
            return true;

        return Roll(shot, cover.Owner) < comp.BlockChance;
    }

    public bool IsShotStopped(EntityUid cover, EntityUid shot, EntityUid? shooter, float? distance = null,
        EntityUid? aimedAt = null, Vector2? shotDirection = null)
        => TryComp<ProjectileCoverComponent>(cover, out var comp)
        && IsShotStopped((cover, comp), shot, shooter, distance, aimedAt, shotDirection);

    public bool PassesOverCover(EntityUid cover, EntityUid shot, EntityUid? shooter, float? distance = null,
        EntityUid? aimedAt = null, Vector2? shotDirection = null)
        => TryComp<ProjectileCoverComponent>(cover, out var comp)
        && !IsShotStopped((cover, comp), shot, shooter, distance, aimedAt, shotDirection);

    public bool IsShelteredFromShot(EntityUid target, Vector2 shotDirection)
    {
        if (!_standing.IsDown(target))
            return false;

        foreach (var cover in _lookup.GetEntitiesInRange<ProjectileCoverComponent>(Transform(target).Coordinates, ShelterSearchRange))
        {
            if (cover.Comp.BlockChance > 0f && IsSheltering(cover, target, shotDirection))
                return true;
        }

        return false;
    }

    private bool IsSheltering(Entity<ProjectileCoverComponent> cover, EntityUid target, Vector2 shotDirection)
    {
        if (!_standing.IsDown(target))
            return false;

        var coverPos = _transform.GetMapCoordinates(cover);
        var targetPos = _transform.GetMapCoordinates(target);

        if (coverPos.MapId != targetPos.MapId)
            return false;

        var offset = targetPos.Position - coverPos.Position;

        if (offset.Length() > cover.Comp.ShelterRange)
            return false;

        var along = Vector2.Dot(offset, shotDirection);
        var across = MathF.Abs(offset.X * shotDirection.Y - offset.Y * shotDirection.X);

        return along > 0f && across <= ShelterLineTolerance;
    }

    private static Vector2? Direction(Vector2 velocity)
        => velocity.LengthSquared() > 0.0001f ? Vector2.Normalize(velocity) : null;

    private bool IsPointBlank(EntityUid cover, EntityUid? shooter, float? distance)
    {
        if (!TryComp<ProjectileCoverComponent>(cover, out var comp) || comp.PointBlankRange <= 0f)
            return false;

        if (distance is { } travelled)
            return travelled <= comp.PointBlankRange;

        if (shooter is not { } user || TerminatingOrDeleted(user))
            return false;

        var coverPos = _transform.GetMapCoordinates(cover);
        var shooterPos = _transform.GetMapCoordinates(user);

        if (coverPos.MapId != shooterPos.MapId)
            return false;

        return (coverPos.Position - shooterPos.Position).Length() <= comp.PointBlankRange;
    }

    private float Roll(EntityUid shot, EntityUid cover)
    {
        var seed = HashCode.Combine(GetNetEntity(shot).Id, GetNetEntity(cover).Id);

        var hash = (uint)seed;
        hash ^= hash >> 16;
        hash *= 0x7feb352d;
        hash ^= hash >> 15;
        hash *= 0x846ca68b;
        hash ^= hash >> 16;

        return hash / (float)uint.MaxValue;
    }
}
