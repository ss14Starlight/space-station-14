using Robust.Client.Graphics;

namespace Content.Client._Starlight.Overlay.Overlays;

public sealed class NightVisionOverlay : BaseVisionOverlay
{
    public NightVisionOverlay(ShaderPrototype shader) : base(shader)
        => ZIndex = (int?)OverlayZIndexes.NightVision;
}
