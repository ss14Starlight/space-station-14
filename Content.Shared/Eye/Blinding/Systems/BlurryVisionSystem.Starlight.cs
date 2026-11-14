using Content.Shared.Eye.Blinding.Components;

namespace Content.Shared.Eye.Blinding.Systems;

public sealed partial class BlurryVisionSystem
{
    public void UpdateBlurMagnitude(Entity<BlindableComponent?> ent)
    {
        var ev = new GetBlurEvent(0);
        RaiseLocalEvent(ent, ev);
        UpdateBlurMagnitude(ent, ev.HasVisionCorrection);
    }
}
