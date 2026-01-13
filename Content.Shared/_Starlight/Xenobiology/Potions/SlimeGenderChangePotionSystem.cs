using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Robust.Shared.Enums;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeGenderChangePotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _sharedHumanoidAppearanceSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeGenderChangePotionComponent, AfterInteractEvent>(OnAfterInteract);
    }
    
    private void OnAfterInteract(Entity<SlimeGenderChangePotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        if (!_entityManager.TryGetComponent<HumanoidAppearanceComponent>(args.Target.Value,
                out var humanoidAppearanceComponent)) return;
        // Because there are 4 gender options, this potion will simply cycle through each one
        // It would be better to have a dropdown list of genders, but this works for now
        var gender = humanoidAppearanceComponent.Gender;
        var nextGender = gender switch
        {
            Gender.Neuter => Gender.Epicene,
            Gender.Epicene => Gender.Female,
            Gender.Female => Gender.Male,
            Gender.Male => Gender.Neuter,
            _ => throw new ArgumentOutOfRangeException(nameof(gender), $"Unexpected gender in SlimeGenderChangePotionComponent interaction: {gender}")
        };
        _sharedHumanoidAppearanceSystem.SetGender((args.Target.Value, humanoidAppearanceComponent), nextGender);
        PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}