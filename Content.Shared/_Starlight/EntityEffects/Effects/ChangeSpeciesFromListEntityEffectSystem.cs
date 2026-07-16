using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.EntityEffects.Effects;

public sealed partial class ChangeSpeciesFromListEntityEffectSystem : EntityEffectSystem<HumanoidAppearanceComponent, ChangeSpeciesFromList>
{
    [Dependency] private SharedHumanoidAppearanceSystem _sharedHumanoidAppearanceSystem = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;

    protected override void Effect(Entity<HumanoidAppearanceComponent> entity,
        ref EntityEffectEvent<ChangeSpeciesFromList> args)
    {
        var newSpecies = _robustRandom.Pick(args.Effect.SpeciesList);
        _sharedHumanoidAppearanceSystem.SetSpecies(entity.Owner, newSpecies, true, entity.AsNullable());
    }
}
