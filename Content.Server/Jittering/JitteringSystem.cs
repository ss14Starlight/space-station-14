using Content.Shared.Jittering;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Jittering
{
    public sealed class JitteringSystem : SharedJitteringSystem
    {
        [Dependency] private StatusEffectsSystem _statusEffects = default!;

        // Starlight DoRemoveJitter
        public override void DoRemoveJitter(EntityUid uid)
            => _statusEffects.TryRemoveStatusEffect(uid, "Jitter");
    }
}
