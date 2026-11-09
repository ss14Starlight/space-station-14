using System.Numerics;
using Content.Shared._Starlight.Random;
using Content.Shared._Starlight.Weapons.Cover.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Starlight.Weapons.Cover.Systems;

public sealed partial class SharedProjectileCoverSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
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

        // Someone shooting out of the crate or locker they hide in hits its walls, it is no cover for them.
        if (projectile.Shooter is { } shooter
            && !TerminatingOrDeleted(shooter)
            && _container.TryGetOuterContainer(shooter, Transform(shooter), out var container)
            && container.Owner == cover.Owner)
        {
            return;
        }

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

        if (Direction(args.OurBody.LinearVelocity) is { } direction && TryGetShelter(args.OtherEntity, direction, out _))
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
        Vector2? shotDirection = null,
        int? seed = null)
    {
        var comp = cover.Comp;

        if (!CanShelter(comp))
            return false;

        if (_whitelist.IsWhitelistFail(comp.Whitelist, shot) || _whitelist.IsWhitelistPass(comp.Blacklist, shot))
            return false;

        if (aimedAt == cover.Owner)
            return true;

        var isSheltering = aimedAt is { } target
            && shotDirection is { } direction
            && IsSheltering(cover, target, direction);

        if (isSheltering && comp.ProneAlwaysBlock)
            return true;

        if (comp.ProneOnly && (aimedAt is not { } target1 || !_standing.IsDown(target1) || !isSheltering))
            return false;

        if (IsPointBlank(cover, shooter, distance))
            return false;

        if (comp.BlockChance >= 1f)
            return true;

        // A predicted hitscan is a throwaway client entity with its own NetEntity id, so the shot seed
        // replaces the shot id when given: client and server have to roll the same value.
        return DeterministicRandom.Prob(
            comp.BlockChance,
            seed ?? GetNetEntity(shot).Id,
            GetNetEntity(cover).Id);
    }

    public bool IsShotStopped(EntityUid cover, EntityUid shot, EntityUid? shooter, float? distance = null,
        EntityUid? aimedAt = null, Vector2? shotDirection = null, int? seed = null)
        => TryComp<ProjectileCoverComponent>(cover, out var comp)
        && IsShotStopped((cover, comp), shot, shooter, distance, aimedAt, shotDirection, seed);

    public bool PassesOverCover(EntityUid cover, EntityUid shot, EntityUid? shooter, float? distance = null,
        EntityUid? aimedAt = null, Vector2? shotDirection = null, int? seed = null)
        => TryComp<ProjectileCoverComponent>(cover, out var comp)
        && !IsShotStopped((cover, comp), shot, shooter, distance, aimedAt, shotDirection, seed);

    public bool IsShelteredFromShot(EntityUid target, Vector2 shotDirection)
        => TryGetShelter(target, shotDirection, out _);

    /// <summary>
    /// Finds the cover a lying target hides behind from a shot flying in <paramref name="shotDirection"/>.
    /// </summary>
    public bool TryGetShelter(EntityUid target, Vector2 shotDirection, out EntityUid shelter)
    {
        foreach (var cover in GetShelters(target, shotDirection))
        {
            shelter = cover;
            return true;
        }

        shelter = default;
        return false;
    }

    /// <summary>
    /// Every cover a lying target hides behind from a shot flying in <paramref name="shotDirection"/>.
    /// Callers that have the shot itself still have to check it with <see cref="IsShotStopped(EntityUid, EntityUid, EntityUid?, float?, EntityUid?, Vector2?, int?)"/>.
    /// </summary>
    public IEnumerable<EntityUid> GetShelters(EntityUid target, Vector2 shotDirection)
    {
        if (!_standing.IsDown(target))
            yield break;

        foreach (var cover in _lookup.GetEntitiesInRange<ProjectileCoverComponent>(Transform(target).Coordinates, ShelterSearchRange))
        {
            if (cover.Owner == target || !CanShelter(cover.Comp) || !IsSheltering(cover, target, shotDirection))
                continue;

            yield return cover.Owner;
        }
    }

    private static bool CanShelter(ProjectileCoverComponent comp)
        => (comp.ProneOnly && comp.ProneAlwaysBlock) || comp.BlockChance > 0f;

    private bool IsSheltering(Entity<ProjectileCoverComponent> cover, EntityUid target, Vector2 shotDirection)
    {
        if (cover.Comp.ProneAlwaysBlock && !_standing.IsDown(target))
            return false;

        var coverPos = _transform.GetMapCoordinates(cover);
        var targetPos = _transform.GetMapCoordinates(target);

        if (coverPos.MapId != targetPos.MapId)
            return false;

        var offset = targetPos.Position - coverPos.Position;

        if (offset.Length() > cover.Comp.ShelterRange)
            return false;

        var along = Vector2.Dot(offset, shotDirection);
        var across = MathF.Abs((offset.X * shotDirection.Y) - (offset.Y * shotDirection.X));

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
}
