using Content.Shared.Tag;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem
{
    private static readonly ProtoId<TagPrototype> PersonalEnergyDomeTag = "PersonalEnergyDome";

    private bool IsPersonalEnergyDome(EntityUid other)
        => _tag.HasTag(other, PersonalEnergyDomeTag);
}
