using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sol.EntityEffects.Effects;

/// <summary>
/// Shortens an active severe+ allergic reaction. Used by antihistamine / epinephrine.
/// </summary>
public sealed partial class ShortenAllergyReaction : EntityEffectBase<ShortenAllergyReaction>
{
    /// <summary>
    /// Base seconds removed from the reaction per effect application (scaled by metabolism).
    /// </summary>
    [DataField]
    public float Seconds = 8f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc)
    {
        return loc.GetString("entity-effect-guidebook-shorten-allergy-reaction",
            ("seconds", Seconds),
            ("chance", Probability));
    }
}
