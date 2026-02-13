using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared._Starlight.Cybernetics.Components;
using Robust.Shared.Random;
using Content.Shared.Humanoid;

namespace Content.Shared._Starlight.Cybernetics;

public sealed class HitscanCyberneticDisruptionSystem : EntitySystem
{
    [Dependency] private readonly SharedCyberneticDisruptionSystem _disrupt = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanCyberneticDisruptionComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanCyberneticDisruptionComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        // you can't disrupt cybernetics on things which cannot have cybernetics in the first place
        if (!HasComp<HumanoidAppearanceComponent>(args.Data.HitEntity))
            return;

        if(_random.NextFloat() <= hitscan.Comp.DisableChance)
            _disrupt.TryAddCyberneticDisruptionDuration(args.Data.HitEntity.Value, hitscan.Comp.Duration);
    }
}
