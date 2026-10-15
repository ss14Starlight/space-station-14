using Content.Shared._Starlight.FireDragon;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Starlight.FireDragon;

public sealed class PredatorHuntOverlay : Robust.Client.Graphics.Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    public PredatorHuntStatusEffectComponent? Effect;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Effect is not { } effect)
            return;

        var width = (float) args.ViewportBounds.Width;
        var height = (float) args.ViewportBounds.Height;
        // A subtle ember-colored border leaves the center of the screen clear.
        for (var band = 0; band < effect.OverlayBands; band++)
        {
            var thickness = (effect.OverlayBands - band) * height * effect.OverlayBandThickness;
            var color = effect.OverlayColor;
            args.ScreenHandle.DrawRect(new UIBox2(0, 0, width, thickness), color);
            args.ScreenHandle.DrawRect(new UIBox2(0, height - thickness, width, height), color);
            args.ScreenHandle.DrawRect(new UIBox2(0, thickness, thickness, height - thickness), color);
            args.ScreenHandle.DrawRect(new UIBox2(width - thickness, thickness, width, height - thickness), color);
        }
    }
}
