using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Jittering
{
    /// <summary>
    ///     A system for applying a jitter animation to any entity.
    /// </summary>
    public abstract partial class SharedJitteringSystem : EntitySystem
    {
        public static readonly EntProtoId JitterEffectId = "StatusEffectJitter";

        [Dependency] protected IGameTiming GameTiming = default!;
        [Dependency] protected StatusEffectsSystem StatusEffects = default!;

        public float MaxAmplitude = 300f;
        public float MinAmplitude = 1f;

        public float MaxFrequency = 10f;
        public float MinFrequency = 1f;

        public override void Initialize()
        {
            SubscribeLocalEvent<JitteringComponent, RejuvenateEvent>(OnRejuvenate);
            SubscribeLocalEvent<JitterStatusEffectComponent, StatusEffectAppliedEvent>(OnJitterApplied);
            SubscribeLocalEvent<JitterStatusEffectComponent, StatusEffectRemovedEvent>(OnJitterRemoved);
        }

        private void OnRejuvenate(EntityUid uid, JitteringComponent component, RejuvenateEvent args)
        {
            RemCompDeferred<JitteringComponent>(uid);
        }

        private void OnJitterApplied(Entity<JitterStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
        {
            EnsureComp<JitteringComponent>(args.Target);
        }

        private void OnJitterRemoved(Entity<JitterStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
        {
            RemComp<JitteringComponent>(args.Target);
        }

        /// <summary>
        ///     Applies a jitter effect to the specified entity.
        ///     You can apply this to any entity whatsoever, so be careful what you use it on!
        /// </summary>
        /// <remarks>
        ///     If the entity is already jittering, the jitter values will be updated but only if they're greater
        ///     than the current ones and <see cref="forceValueChange"/> is false.
        /// </remarks>
        /// <param name="uid">Entity in question.</param>
        /// <param name="time">For how much time to apply the effect.</param>
        /// <param name="refresh">The status effect cooldown should be refreshed (true) or accumulated (false).</param>
        /// <param name="amplitude">Jitteriness of the animation. See <see cref="MaxAmplitude"/> and <see cref="MinAmplitude"/>.</param>
        /// <param name="frequency">Frequency for jittering. See <see cref="MaxFrequency"/> and <see cref="MinFrequency"/>.</param>
        /// <param name="forceValueChange">Whether to change any existing jitter value even if they're greater than the ones we're setting.</param>
        public void DoJitter(EntityUid uid, TimeSpan time, bool refresh, float amplitude = 10f, float frequency = 4f, bool forceValueChange = false)
        {
            amplitude = Math.Clamp(amplitude, MinAmplitude, MaxAmplitude);
            frequency = Math.Clamp(frequency, MinFrequency, MaxFrequency);

            var applied = refresh
                ? StatusEffects.TryUpdateStatusEffectDuration(uid, JitterEffectId, time)
                : StatusEffects.TryAddStatusEffectDuration(uid, JitterEffectId, time);

            if (!applied)
                return;

            var jittering = EnsureComp<JitteringComponent>(uid);

            if (forceValueChange || jittering.Amplitude < amplitude)
                jittering.Amplitude = amplitude;

            if (forceValueChange || jittering.Frequency < frequency)
                jittering.Frequency = frequency;

            Dirty(uid, jittering);
        }

        /// <summary>
        /// For non mobs.
        /// </summary>
        public void AddJitter(EntityUid uid, float amplitude = 10f, float frequency = 4f)
        {
            var jitter = EnsureComp<JitteringComponent>(uid);
            jitter.Amplitude = amplitude;
            jitter.Frequency = frequency;
            Dirty(uid, jitter);
        }
    }
}
