using Content.Shared.Eye.Blinding.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Shared._FarHorizons.StatusEffects.Blindness;

public abstract class SharedBlindnessSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlindnessStatusEffectComponent, StatusEffectAppliedEvent>(OnEffectApplied);
        SubscribeLocalEvent<BlindnessStatusEffectComponent, StatusEffectRemovedEvent>(OnEffectRemoved);
    }

    private void OnEffectApplied(Entity<BlindnessStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (!_gameTiming.ApplyingState)
            EnsureComp<TemporaryBlindnessComponent>(args.Target);
    }
    private void OnEffectRemoved(Entity<BlindnessStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (TryComp<TemporaryBlindnessComponent>(args.Target, out var blind))
            RemCompDeferred(args.Target, blind);
    }
}