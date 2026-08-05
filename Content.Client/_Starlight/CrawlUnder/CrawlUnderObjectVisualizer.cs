using Content.Shared._Starlight.CrawlUnder;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Starlight.CrawlUnder;

public sealed partial class CrawlUnderObjectsVisualizer : VisualizerSystem<CrawlUnderObjectsComponent>
{
    private const DrawDepth CrawlDrawDepth = DrawDepth.SmallMobs;

    protected override void OnAppearanceChange(EntityUid uid, CrawlUnderObjectsComponent component, ref AppearanceChangeEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        AppearanceSystem.TryGetData(uid, SneakMode.Enabled, out bool enabled);

        if (enabled)
        {
            if (component.OriginalDrawDepth != null)
                return;

            component.OriginalDrawDepth = sprite.DrawDepth;
            SpriteSystem.SetDrawDepth((uid, sprite), (int) CrawlDrawDepth);
        }
        else
        {
            if (component.OriginalDrawDepth == null)
                return;

            SpriteSystem.SetDrawDepth((uid, sprite), (int) component.OriginalDrawDepth);
            component.OriginalDrawDepth = null;
        }
    }
}
