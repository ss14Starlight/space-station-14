using System.Numerics;
using Content.Shared._Starlight.TwistyCube;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.TwistyCube;

public sealed partial class TwistyCubeControl : Control
{
    private static readonly ResPath ViewRsi = new("_Starlight/TwistyCube/ui_view.rsi");

    [Dependency] private IResourceCache _resourceCache = default!;

    public TwistyCubeControl()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new(512, 256);
    }

    public TwistyCubeState CubeState = new();

    protected override void Draw(DrawingHandleScreen handle)
    {
        void DrawPart(string name, TwistyCubeColor side)
        {
            if (TryGetRsiFrame(ViewRsi, name) is {} t)
                handle.DrawTextureRect(t, UIBox2.FromDimensions(Vector2.Zero, new(512, 256)), side.AsColor());
        }
        
        DrawPart("ktl-1", CubeState.FrontTopLeft.Side1);
        DrawPart("ktl-2", CubeState.FrontTopLeft.Side2);
        DrawPart("ktl-3", CubeState.FrontTopLeft.Side3);
        DrawPart("kt-1", CubeState.FrontTop.Side1);
        DrawPart("kt-2", CubeState.FrontTop.Side2);
        DrawPart("ktr-1", CubeState.FrontTopRight.Side1);
        DrawPart("ktr-2", CubeState.FrontTopRight.Side2);
        DrawPart("ktr-3", CubeState.FrontTopRight.Side3);
        
        DrawPart("kl-1", CubeState.FrontLeft.Side1);
        DrawPart("kl-2", CubeState.FrontLeft.Side2);
        DrawPart("k", TwistyCubeColor.Front);
        DrawPart("kr-1", CubeState.FrontRight.Side1);
        DrawPart("kr-2", CubeState.FrontRight.Side2);
        
        DrawPart("kbl-1", CubeState.FrontBottomLeft.Side1);
        DrawPart("kbl-2", CubeState.FrontBottomLeft.Side2);
        DrawPart("kbl-3", CubeState.FrontBottomLeft.Side3);
        DrawPart("kb-1", CubeState.FrontBottom.Side1);
        DrawPart("kb-2", CubeState.FrontBottom.Side2);
        DrawPart("kbr-1", CubeState.FrontBottomRight.Side1);
        DrawPart("kbr-2", CubeState.FrontBottomRight.Side2);
        DrawPart("kbr-3", CubeState.FrontBottomRight.Side3);
        
        DrawPart("tl-1", CubeState.TopLeft.Side1);
        DrawPart("tl-2", CubeState.TopLeft.Side2);
        DrawPart("t", TwistyCubeColor.Top);
        DrawPart("tr-1", CubeState.TopRight.Side1);
        DrawPart("tr-2", CubeState.TopRight.Side2);
        
        DrawPart("l", TwistyCubeColor.Left);
        DrawPart("r", TwistyCubeColor.Right);

        DrawPart("bl-1", CubeState.BottomLeft.Side1);
        DrawPart("bl-2", CubeState.BottomLeft.Side2);
        DrawPart("b", TwistyCubeColor.Bottom);
        DrawPart("br-1", CubeState.BottomRight.Side1);
        DrawPart("br-2", CubeState.BottomRight.Side2);
        
        DrawPart("ktl-1", CubeState.BackTopLeft.Side1);
        DrawPart("ktl-2", CubeState.BackTopLeft.Side2);
        DrawPart("ktl-3", CubeState.BackTopLeft.Side3);
        DrawPart("kt-1", CubeState.BackTop.Side1);
        DrawPart("kt-2", CubeState.BackTop.Side2);
        DrawPart("ktr-1", CubeState.BackTopRight.Side1);
        DrawPart("ktr-2", CubeState.BackTopRight.Side2);
        DrawPart("ktr-3", CubeState.BackTopRight.Side3);
        
        DrawPart("kl-1", CubeState.BackLeft.Side1);
        DrawPart("kl-2", CubeState.BackLeft.Side2);
        DrawPart("k", TwistyCubeColor.Back);
        DrawPart("kr-1", CubeState.BackRight.Side1);
        DrawPart("kr-2", CubeState.BackRight.Side2);
        
        DrawPart("kbl-1", CubeState.BackBottomLeft.Side1);
        DrawPart("kbl-2", CubeState.BackBottomLeft.Side2);
        DrawPart("kbl-3", CubeState.BackBottomLeft.Side3);
        DrawPart("kb-1", CubeState.BackBottom.Side1);
        DrawPart("kb-2", CubeState.BackBottom.Side2);
        DrawPart("kbr-1", CubeState.BackBottomRight.Side1);
        DrawPart("kbr-2", CubeState.BackBottomRight.Side2);
        DrawPart("kbr-3", CubeState.BackBottomRight.Side3);
    }


    private Texture? TryGetRsiFrame(ResPath rsiPath, string stateName)
    {
        if (!_resourceCache.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / rsiPath, out var rsiRes))
            return null;

        if (!rsiRes.RSI.TryGetState(stateName, out var state))
            return null;

        return state.GetFrame(RsiDirection.South, 0);
    }
}