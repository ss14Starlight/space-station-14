using Content.Shared._Starlight.Movement.Components;
using Content.Shared.Movement.Events;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Movement.Systems;

public sealed class RandomFootstepSoundSystem : EntitySystem
{

    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomFootstepSoundComponent, GetFootstepSoundEvent>(OnGetFootstepSound);
    }

    private void OnGetFootstepSound(Entity<RandomFootstepSoundComponent> ent, ref GetFootstepSoundEvent ev)
    {
        if (_random.Prob(ent.Comp.Chance))
            ev.Sound = ent.Comp.Sound;
    }
}
