using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;

// ReSharper disable once CheckNamespace
namespace Content.Client.Buckle;

internal sealed partial class BuckleSystem
{
    private void UpdateVisibility(Entity<BuckleComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
        var buckled = Appearance.TryGetData<bool>(ent, BuckleVisuals.Buckled, out var value, args.Component) && value;
        var hidden = buckled && ent.Comp.BuckledTo is { } strapUid &&
            TryComp<StrapComponent>(strapUid, out var strap) && strap.SetVisible;
        if (hidden)
        {
            ent.Comp.OriginalVisible ??= args.Sprite.Visible;
            _sprite.SetVisible((ent.Owner, args.Sprite), false);
        }
        else if (ent.Comp.OriginalVisible is { } visible)
        {
            _sprite.SetVisible((ent.Owner, args.Sprite), visible);
            ent.Comp.OriginalVisible = null;
        }
    }
}
