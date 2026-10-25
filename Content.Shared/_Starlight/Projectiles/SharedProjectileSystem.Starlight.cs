using Content.Shared.Tag;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem
{
    private static readonly ProtoId<TagPrototype> PersonalEnergyDomeTag = "PersonalEnergyDome";

    private bool IgnoresPersonalEnergyDome(ProjectileComponent projectile, EntityUid other)
    {
        return projectile.IgnoreShooter && projectile.Shooter != null &&
               _tag.HasTag(other, PersonalEnergyDomeTag) &&
               Transform(other).ParentUid == projectile.Shooter;
    }
}
