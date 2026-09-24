using Content.Shared._Starlight.TwistyCube;
using Content.Shared.Item;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.TwistyCube;

public sealed partial class TwistyCubeVisualsSystem : VisualizerSystem<TwistyCubeComponent>
{
    [Dependency] private SharedItemSystem _item = default!;

    protected override void OnAppearanceChange(EntityUid uid, TwistyCubeComponent component, ref AppearanceChangeEvent args)
    {
        var cubeState = component.State;
        
        SpriteSystem.LayerSetColor((uid, args.Sprite), "base", Color.White);
        
        SpriteSystem.LayerSetColor((uid, args.Sprite), "ftl-1", cubeState.FrontTopLeft.Side1.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "ftl-2", cubeState.FrontTopLeft.Side2.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "ftl-3", cubeState.FrontTopLeft.Side3.AsColor());

        SpriteSystem.LayerSetColor((uid, args.Sprite), "ft-1", cubeState.FrontTop.Side1.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "ft-2", cubeState.FrontTop.Side2.AsColor());
        
        SpriteSystem.LayerSetColor((uid, args.Sprite), "ftr-1", cubeState.FrontTopRight.Side1.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "ftr-2", cubeState.FrontTopRight.Side2.AsColor());
        
        SpriteSystem.LayerSetColor((uid, args.Sprite), "fl-1", cubeState.FrontLeft.Side1.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "fl-2", cubeState.FrontLeft.Side2.AsColor());
        
        SpriteSystem.LayerSetColor((uid, args.Sprite), "f", TwistyCubeColor.Front.AsColor());
        
        SpriteSystem.LayerSetColor((uid, args.Sprite), "fr-1", cubeState.FrontRight.Side1.AsColor());
        
        SpriteSystem.LayerSetColor((uid, args.Sprite), "fbl-1", cubeState.FrontBottomLeft.Side1.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "fbl-3", cubeState.FrontBottomLeft.Side3.AsColor());

        SpriteSystem.LayerSetColor((uid, args.Sprite), "fb-1", cubeState.FrontBottom.Side1.AsColor());
        
        SpriteSystem.LayerSetColor((uid, args.Sprite), "fbr-1", cubeState.FrontBottomRight.Side1.AsColor());

        SpriteSystem.LayerSetColor((uid, args.Sprite), "bl-2", cubeState.BottomLeft.Side2.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "l", TwistyCubeColor.Left.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "tl-2", cubeState.TopLeft.Side2.AsColor());
        
        SpriteSystem.LayerSetColor((uid, args.Sprite), "tl-1", cubeState.TopLeft.Side1.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "t", TwistyCubeColor.Top.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "tr-1", cubeState.TopRight.Side1.AsColor());

        SpriteSystem.LayerSetColor((uid, args.Sprite), "kbl-3", cubeState.BackBottomLeft.Side3.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "kl-2", cubeState.BackLeft.Side2.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "ktl-3", cubeState.BackTopLeft.Side3.AsColor());

        SpriteSystem.LayerSetColor((uid, args.Sprite), "ktl-2", cubeState.BackTopLeft.Side2.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "kt-2", cubeState.BackTop.Side2.AsColor());
        SpriteSystem.LayerSetColor((uid, args.Sprite), "ktr-2", cubeState.BackTopRight.Side2.AsColor());
        
        _item.VisualsChanged(uid);
    }
}