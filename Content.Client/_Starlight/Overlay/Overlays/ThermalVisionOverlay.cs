using Robust.Client.Graphics;
using Content.Shared._Starlight.Overlay.Components;

namespace Content.Client._Starlight.Overlay.Overlays;

public sealed class ThermalVisionOverlay : BaseVisionOverlay
{
    private readonly ShaderInstance _alternativeShader;

    public ThermalVisionOverlay(ShaderPrototype shader, ShaderPrototype alternativeShader) : base(shader)
    {
        _alternativeShader = alternativeShader.InstanceUnique();
        ZIndex = (int?)OverlayZIndexes.ThermalVision;
    }

    protected override ShaderInstance GetShader(EntityUid viewer)
        => EntityManager.TryGetComponent<ThermalVisionComponent>(viewer, out var component)
            && component.UseAlternativeShader
                ? _alternativeShader
                : base.GetShader(viewer);
}
