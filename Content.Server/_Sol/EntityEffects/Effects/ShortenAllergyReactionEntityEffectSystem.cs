using Content.Shared._Sol.EntityEffects.Effects;
using Content.Shared._Sol.Medical.Allergy;
using Content.Shared.EntityEffects;
using Robust.Shared.Timing;

namespace Content.Server._Sol.EntityEffects.Effects;

/// <summary>
/// Reduces remaining allergic reaction duration; clears the reaction when it expires.
/// </summary>
public sealed partial class ShortenAllergyReactionEntityEffectSystem
    : EntityEffectSystem<ActiveAllergyReactionComponent, ShortenAllergyReaction>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override void Effect(
        Entity<ActiveAllergyReactionComponent> entity,
        ref EntityEffectEvent<ShortenAllergyReaction> args)
    {
        var seconds = args.Effect.Seconds * args.Scale;
        if (seconds <= 0f)
            return;

        entity.Comp.EndsAt -= TimeSpan.FromSeconds(seconds);
        Dirty(entity);

        if (entity.Comp.EndsAt <= _timing.CurTime)
            RemCompDeferred<ActiveAllergyReactionComponent>(entity);
    }
}
