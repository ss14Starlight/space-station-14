using Content.Shared._Starlight.Weapons.Cover.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Starlight.Weapons.Cover.Systems;

public sealed partial class SharedProjectileCoverSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileCoverComponent, PreventCollideEvent>(OnPreventCollide);
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

        if (!IsShotStopped(cover, other, projectile.Shooter))
            args.Cancelled = true;
    }

    public void SetBlockChance(Entity<ProjectileCoverComponent> cover, float chance)
    {
        cover.Comp.BlockChance = chance;
        Dirty(cover);
    }

    public bool IsShotStopped(Entity<ProjectileCoverComponent> cover, EntityUid shot, EntityUid? shooter, float? distance = null)
    {
        var comp = cover.Comp;

        if (comp.BlockChance <= 0f)
            return false;

        if (_whitelist.IsWhitelistFail(comp.Whitelist, shot) || _whitelist.IsWhitelistPass(comp.Blacklist, shot))
            return false;

        if (IsPointBlank(cover, shooter, distance))
            return false;

        if (comp.BlockChance >= 1f)
            return true;

        return Roll(shot, cover.Owner) < comp.BlockChance;
    }

    public bool IsShotStopped(EntityUid cover, EntityUid shot, EntityUid? shooter, float? distance = null)
        => TryComp<ProjectileCoverComponent>(cover, out var comp)
           && IsShotStopped((cover, comp), shot, shooter, distance);

    public bool PassesOverCover(EntityUid cover, EntityUid shot, EntityUid? shooter, float? distance = null)
        => TryComp<ProjectileCoverComponent>(cover, out var comp)
           && !IsShotStopped((cover, comp), shot, shooter, distance);

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
