using Content.Shared.EntityEffects;
using Content.Shared.EntityTable;
using Content.Shared.Humanoid;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.EntityEffects.Effects;

public sealed class ChangeSpeciesFromListEntityEffectSystem : EntityEffectSystem<HumanoidAppearanceComponent, ChangeSpeciesFromList>
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _sharedHumanoidAppearanceSystem = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;

    protected override void Effect(Entity<HumanoidAppearanceComponent> entity,
        ref EntityEffectEvent<ChangeSpeciesFromList> args)
    {
        var newSpecies = _robustRandom.Pick(args.Effect.SpeciesList);
        _sharedHumanoidAppearanceSystem.SetSpecies(entity.Owner, newSpecies, true, entity.AsNullable());
    }
}