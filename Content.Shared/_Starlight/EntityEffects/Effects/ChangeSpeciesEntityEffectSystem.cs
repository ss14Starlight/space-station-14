using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;

namespace Content.Shared._Starlight.EntityEffects.Effects;

/// <summary>
/// This effect changes the target entity's species.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class ChangeSpeciesEntityEffectSystem : EntityEffectSystem<HumanoidAppearanceComponent, ChangeSpecies>
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _sharedHumanoidAppearanceSystem = default!;

    protected override void Effect(Entity<HumanoidAppearanceComponent> entity, ref EntityEffectEvent<ChangeSpecies> args) => _sharedHumanoidAppearanceSystem.SetSpecies(entity.Owner, args.Effect.Species, true, entity.AsNullable());
}