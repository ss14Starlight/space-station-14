using Robust.Client.Graphics;

namespace Content.Client._Starlight.Overlay.Overlays;

public sealed class XenoVisionOverlay : BaseVisionOverlay
{
    public XenoVisionOverlay(ShaderPrototype shader) : base(shader)
        => ZIndex = (int?)OverlayZIndexes.Xeno;
}
