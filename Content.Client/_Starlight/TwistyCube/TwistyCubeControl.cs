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
    private static readonly ResPath _viewRsi = new("_Starlight/Objects/Fun/TwistyCube/ui_view.rsi");

    [Dependency] private IResourceCache _resourceCache = default!;

    /// <summary>
    /// How far in texture pixels the mouse has to be dragged before the drag direction is locked in.
    /// </summary>
    private const float DragThreshold = 14f;

    /// <summary>
    /// How far in stickers along the drag direction the first quarter turn happens.
    /// Each further quarter turn happens one sticker further along.
    /// </summary>
    private const float FirstTurnDistance = 0.8f;

    /// <summary>
    /// How far in stickers the mouse has to move back past a turn's boundary to undo it,
    /// so that hovering right on a boundary doesn't flick the turn on and off.
    /// </summary>
    private const float TurnHysteresis = 0.15f;

    /// <summary>
    /// Screen-space offset in texture pixels of one cubie step along each 3D axis.
    /// X points right, Y points up and Z points towards the front. Both views share this projection.
    /// </summary>
    private static readonly Vector2[] _axisProjections = [new(37, -19), new(0, -38), new(37, 19)];

    /// <summary>
    /// Rough calculation of the cube visualization.
    /// </summary>
    private static readonly (Vector2 Origin, int Axis, int Sign)[] _faces =
    [
        // Left view (front)
        (new Vector2(124.5f, 128.5f), 1, 1),
        (new Vector2(124.5f, 128.5f), 0, -1),
        (new Vector2(124.5f, 128.5f), 2, 1),
        // Right view (back)
        (new Vector2(385.5f, 125.5f), 2, -1),
        (new Vector2(385.5f, 125.5f), 0, 1),
        (new Vector2(385.5f, 126.5f), 1, -1) // The bottom face's art sits a pixel lower than the rest.
    ];

    /// <summary>
    /// The turn that rotates the layer at index [axis, layer + 1] positively (counterclockwise
    /// when looking down the positive axis). The opposite turn is its paired enum variant.
    /// </summary>
    private static readonly TwistyCubeAction[,] _positiveTurns =
    {
        {
            TwistyCubeAction.LeftClockwise, TwistyCubeAction.MClockwise, TwistyCubeAction.RightCounterClockwise
        },
        {
            TwistyCubeAction.BottomClockwise, TwistyCubeAction.ECounterClockwise, TwistyCubeAction.TopCounterClockwise
        },
        {
            TwistyCubeAction.BackClockwise, TwistyCubeAction.SCounterClockwise, TwistyCubeAction.FrontCounterClockwise
        }
    };

    /// <summary>
    /// The whole-cube rotation that turns the cube positively around each axis.
    /// </summary>
    private static readonly TwistyCubeAction[] _positiveRotations =
    [
        TwistyCubeAction.XClockwise,
        TwistyCubeAction.YCounterClockwise,
        TwistyCubeAction.ZCounterClockwise,
    ];

    public event Action<TwistyCubeAction>? OnAction;

    private sealed class DragState
    {
        public required BoundKeyFunction Button;
        public required bool WholeCube;
        public required int[] Cubie;
        public required int Axis;
        public required int Sign;
        public required Vector2 Start;

        public int? MoveAxis;

        public TwistyCubeAction ForwardTurn;

        public int Turns;
    }

    private DragState? _drag;

    public TwistyCubeControl()
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new Vector2(512, 256);
        MouseFilter = MouseFilterMode.Stop;
    }

    public TwistyCubeState CubeState = new();

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (_drag != null)
            return;

        // Left click rotates an axis, right click rotates view.
        var wholeCube = args.Function == EngineKeyFunctions.UIRightClick;
        if (args.Function != EngineKeyFunctions.UIClick && !wholeCube)
            return;

        if (!TryPickSticker(args.RelativePosition, out var sticker))
        {
            if (!wholeCube)
                return;

            sticker = NearestFace(args.RelativePosition);
        }

        _drag = new DragState
        {
            Button = args.Function,
            WholeCube = wholeCube,
            Cubie = sticker.Cubie,
            Axis = sticker.Axis,
            Sign = sticker.Sign,
            Start = args.RelativePosition,
        };
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == _drag?.Button)
            _drag = null;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_drag is not { } drag)
            return;

        var delta = args.RelativePosition - drag.Start;
        var (a, b) = OtherAxes(drag.Axis);
        if (!TrySolve(delta, _axisProjections[a], _axisProjections[b], out var da, out var db))
            return;

        // Lock the drag to whichever in-face direction it moved along first.
        if (drag.MoveAxis == null)
        {
            if (delta.Length() < DragThreshold)
                return;

            drag.MoveAxis = MathF.Abs(da) >= MathF.Abs(db) ? a : b;
            drag.ForwardTurn = GetForwardTurn(drag, drag.MoveAxis.Value);
        }

        var distance = drag.MoveAxis == a ? da : db;
        var backTurn = (TwistyCubeAction) ((int) drag.ForwardTurn ^ 1);

        while (true)
        {
            var next = TurnBoundary(drag.Turns) + (drag.Turns < 0 ? TurnHysteresis : 0);
            var previous = TurnBoundary(drag.Turns - 1) - (drag.Turns > 0 ? TurnHysteresis : 0);

            if (distance >= next)
            {
                drag.Turns++;
                OnAction?.Invoke(drag.ForwardTurn);
            }
            else if (distance < previous)
            {
                drag.Turns--;
                OnAction?.Invoke(backTurn);
            }
            else
            {
                break;
            }
        }
    }

    private static float TurnBoundary(int turns) =>
        turns >= 0 ? turns + FirstTurnDistance : turns + 1 - FirstTurnDistance;

    private static TwistyCubeAction GetForwardTurn(DragState drag, int moveAxis)
    {
        // Black voodoo magic I found on stack overflow to rotate the face based on drag. No I don't know what a levicivita is.
        var rotAxis = 3 - drag.Axis - moveAxis;
        var leviCivita = (moveAxis - drag.Axis + 3) % 3 == 1 ? 1 : -1;
        var rotSign = drag.Sign * leviCivita;

        var action = drag.WholeCube
            ? _positiveRotations[rotAxis]
            : _positiveTurns[rotAxis, drag.Cubie[rotAxis] + 1];

        return rotSign < 0 ? (TwistyCubeAction) ((int) action ^ 1) : action;
    }

    /// <summary>
    /// Finds the face whose center is closest to a point in the texture.
    /// </summary>
    private static (int[] Cubie, int Axis, int Sign) NearestFace(Vector2 point)
    {
        var best = _faces[0];
        var bestDistance = float.MaxValue;
        foreach (var face in _faces)
        {
            var distance = (point - face.Origin - (_axisProjections[face.Axis] * (1.5f * face.Sign))).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = face;
            bestDistance = distance;
        }

        var cubie = new int[3];
        cubie[best.Axis] = best.Sign;
        return (cubie, best.Axis, best.Sign);
    }

    private static bool TryPickSticker(Vector2 point, out (int[] Cubie, int Axis, int Sign) sticker)
    {
        foreach (var (origin, axis, sign) in _faces)
        {
            var (a, b) = OtherAxes(axis);
            var faceCenter = origin + (_axisProjections[axis] * (1.5f * sign));
            if (!TrySolve(point - faceCenter, _axisProjections[a], _axisProjections[b], out var u, out var v)
                || MathF.Abs(u) > 1.5f
                || MathF.Abs(v) > 1.5f)
                continue;

            var cubie = new int[3];
            cubie[axis] = sign;
            cubie[a] = Math.Clamp((int) MathF.Round(u), -1, 1);
            cubie[b] = Math.Clamp((int) MathF.Round(v), -1, 1);
            sticker = (cubie, axis, sign);
            return true;
        }

        sticker = default;
        return false;
    }

    private static (int, int) OtherAxes(int axis) => ((axis + 1) % 3, (axis + 2) % 3);

    /// <summary>
    /// Solves <c>p = u * a + v * b</c> for u and v. Again, thank you black voodoo magicians on stack overflow.
    /// </summary>
    private static bool TrySolve(Vector2 p, Vector2 a, Vector2 b, out float u, out float v)
    {
        var det = (a.X * b.Y) - (a.Y * b.X);
        if (MathF.Abs(det) < 1e-6f)
        {
            u = v = 0;
            return false;
        }

        u = ((p.X * b.Y) - (p.Y * b.X)) / det;
        v = ((a.X * p.Y) - (a.Y * p.X)) / det;
        return true;
    }

    void DrawPart(DrawingHandleScreen handle, string name, TwistyCubeColor side)
    {
        if (TryGetRsiFrame(_viewRsi, name) is {} t)
            handle.DrawTextureRect(t, UIBox2.FromDimensions(Vector2.Zero, new Vector2(512, 256) * UIScale), side.AsColor());
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (TryGetRsiFrame(_viewRsi, "background") is {} t)
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
        DrawPart(handle, "f", CubeState.Front);
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
        DrawPart(handle, "t", CubeState.Top);
        DrawPart(handle, "tr-1", CubeState.TopRight.Side1);
        DrawPart(handle, "tr-2", CubeState.TopRight.Side2);

        DrawPart(handle, "l", CubeState.Left);
        DrawPart(handle, "r", CubeState.Right);

        DrawPart(handle, "bl-1", CubeState.BottomLeft.Side1);
        DrawPart(handle, "bl-2", CubeState.BottomLeft.Side2);
        DrawPart(handle, "b", CubeState.Bottom);
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
        DrawPart(handle, "k", CubeState.Back);
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
