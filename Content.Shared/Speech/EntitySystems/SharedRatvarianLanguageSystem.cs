using Robust.Shared.Prototypes;

namespace Content.Shared.Speech.EntitySystems;

public abstract class SharedRatvarianLanguageSystem : EntitySystem
{
    public static readonly EntProtoId Ratvarian = "StatusEffectRatvarianLanguage";

    public virtual void DoRatvarian(EntityUid uid, TimeSpan time, bool refresh)
    {
    }
}
