using Content.Shared.Jittering;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Jittering
{
    public sealed class JitteringSystem : SharedJitteringSystem
    {
        [Dependency] private StatusEffectsSystem _statusEffects = default!;

        // This entity system only exists on the server so it will be registered, otherwise we can't use SharedJitteringSystem...
        public override void DoRemoveJitter(EntityUid uid)
            => _statusEffects.TryRemoveStatusEffect(uid, "Jitter");
    }
}
