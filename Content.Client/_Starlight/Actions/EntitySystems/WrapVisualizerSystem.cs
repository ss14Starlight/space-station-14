using Content.Shared._Starlight.Actions.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Actions.EntitySystems;

public sealed class WrapVisualizerSystem : VisualizerSystem<WrappedComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, WrappedComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<bool>(uid, WrappedVisuals.IsWrapped, out var isWrapped, args.Component))
            return;

        if (isWrapped)
            SpriteSystem.SetVisible((uid, args.Sprite), false);
        else
            SpriteSystem.SetVisible((uid, args.Sprite), true);
    }
}
