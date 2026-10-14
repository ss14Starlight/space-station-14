using Robust.Client.Graphics;

namespace Content.Client._Starlight.Overlay.Overlays;

public sealed class NullSpaceOverlay : BaseVisionOverlay
{
    public NullSpaceOverlay(ShaderPrototype shader) : base(shader)
        => ZIndex = (int?)OverlayZIndexes.NullSpace;
}
