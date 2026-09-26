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
    private static readonly ResPath ViewRsi = new("_Starlight/Objects/Fun/TwistyCube/ui_view.rsi");

    [Dependency] private IResourceCache _resourceCache = default!;

    public TwistyCubeControl()
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new(512, 256);
    }

    public TwistyCubeState CubeState = new();

    void DrawPart(DrawingHandleScreen handle, string name, TwistyCubeColor side)
    {
        if (TryGetRsiFrame(ViewRsi, name) is {} t)
            handle.DrawTextureRect(t, UIBox2.FromDimensions(Vector2.Zero, new Vector2(512, 256) * UIScale), side.AsColor());
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (TryGetRsiFrame(ViewRsi, "background") is {} t)
            handle.DrawTextureRect(t, UIBox2.FromDimensions(Vector2.Zero, new Vector2(512, 256) * UIScale), Color.Black);

        DrawPart(handle, "ftl-1", CubeState.FrontTopLeft.Side1);
        DrawPart(handle, "ftl-2", CubeState.FrontTopLeft.Side2);
        DrawPart(handle, "ftl-3", CubeState.FrontTopLeft.Side3);
        DrawPart(handle, "ft-1", CubeState.FrontTop.Side1);
        DrawPart(handle, "ft-2", CubeState.FrontTop.Side2);
        DrawPart(handle, "ftr-1", CubeState.FrontTopRight.Side1);
        DrawPart(handle, "ftr-2", CubeState.FrontTopRight.Side2);
        DrawPart(handle, "ftr-3", CubeState.FrontTopRight.Side3);

        DrawPart(handle, "fl-1", CubeState.FrontLeft.Side1);
        DrawPart(handle, "fl-2", CubeState.FrontLeft.Side2);
        DrawPart(handle, "f", TwistyCubeColor.Front);
        DrawPart(handle, "fr-1", CubeState.FrontRight.Side1);
        DrawPart(handle, "fr-2", CubeState.FrontRight.Side2);

        DrawPart(handle, "fbl-1", CubeState.FrontBottomLeft.Side1);
        DrawPart(handle, "fbl-2", CubeState.FrontBottomLeft.Side2);
        DrawPart(handle, "fbl-3", CubeState.FrontBottomLeft.Side3);
        DrawPart(handle, "fb-1", CubeState.FrontBottom.Side1);
        DrawPart(handle, "fb-2", CubeState.FrontBottom.Side2);
        DrawPart(handle, "fbr-1", CubeState.FrontBottomRight.Side1);
        DrawPart(handle, "fbr-2", CubeState.FrontBottomRight.Side2);
        DrawPart(handle, "fbr-3", CubeState.FrontBottomRight.Side3);

        DrawPart(handle, "tl-1", CubeState.TopLeft.Side1);
        DrawPart(handle, "tl-2", CubeState.TopLeft.Side2);
        DrawPart(handle, "t", TwistyCubeColor.Top);
        DrawPart(handle, "tr-1", CubeState.TopRight.Side1);
        DrawPart(handle, "tr-2", CubeState.TopRight.Side2);

        DrawPart(handle, "l", TwistyCubeColor.Left);
        DrawPart(handle, "r", TwistyCubeColor.Right);

        DrawPart(handle, "bl-1", CubeState.BottomLeft.Side1);
        DrawPart(handle, "bl-2", CubeState.BottomLeft.Side2);
        DrawPart(handle, "b", TwistyCubeColor.Bottom);
        DrawPart(handle, "br-1", CubeState.BottomRight.Side1);
        DrawPart(handle, "br-2", CubeState.BottomRight.Side2);

        DrawPart(handle, "ktl-1", CubeState.BackTopLeft.Side1);
        DrawPart(handle, "ktl-2", CubeState.BackTopLeft.Side2);
        DrawPart(handle, "ktl-3", CubeState.BackTopLeft.Side3);
        DrawPart(handle, "kt-1", CubeState.BackTop.Side1);
        DrawPart(handle, "kt-2", CubeState.BackTop.Side2);
        DrawPart(handle, "ktr-1", CubeState.BackTopRight.Side1);
        DrawPart(handle, "ktr-2", CubeState.BackTopRight.Side2);
        DrawPart(handle, "ktr-3", CubeState.BackTopRight.Side3);

        DrawPart(handle, "kl-1", CubeState.BackLeft.Side1);
        DrawPart(handle, "kl-2", CubeState.BackLeft.Side2);
        DrawPart(handle, "k", TwistyCubeColor.Back);
        DrawPart(handle, "kr-1", CubeState.BackRight.Side1);
        DrawPart(handle, "kr-2", CubeState.BackRight.Side2);

        DrawPart(handle, "kbl-1", CubeState.BackBottomLeft.Side1);
        DrawPart(handle, "kbl-2", CubeState.BackBottomLeft.Side2);
        DrawPart(handle, "kbl-3", CubeState.BackBottomLeft.Side3);
        DrawPart(handle, "kb-1", CubeState.BackBottom.Side1);
        DrawPart(handle, "kb-2", CubeState.BackBottom.Side2);
        DrawPart(handle, "kbr-1", CubeState.BackBottomRight.Side1);
        DrawPart(handle, "kbr-2", CubeState.BackBottomRight.Side2);
        DrawPart(handle, "kbr-3", CubeState.BackBottomRight.Side3);
    }

    private Texture? TryGetRsiFrame(ResPath rsiPath, string stateName)
    {
        if (!_resourceCache.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / rsiPath, out var rsiRes))
            return null;

        return rsiRes.RSI.TryGetState(stateName, out var state) ? state.Frame0 : null;
    }
}