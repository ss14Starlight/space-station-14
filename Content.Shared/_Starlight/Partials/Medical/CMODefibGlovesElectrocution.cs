// ReSharper disable CheckNamespace
using Content.Shared.StatusEffect;

namespace Content.Shared.Electrocution;

public abstract partial class SharedElectrocutionSystem
{
    /// <summary>
    /// Applies the glove shock's normal electrocution effects, but knocks the target down without dropping held items.
    /// </summary>
    public virtual bool TryDoGloveElectrocution(
        EntityUid uid, EntityUid? sourceUid, int shockDamage, TimeSpan time, TimeSpan knockdownDuration, bool refresh,
        float siemensCoefficient = 1f, StatusEffectsComponent? statusEffects = null, bool ignoreInsulation = false)
    {
        return false;
    }
}
