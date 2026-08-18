using System.Numerics;
using Content.Shared._Starlight.Arcade.Lancer;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Arcade.Lancer;

public sealed class LancerBoardControl : Control
{
    private const float HexSize = 28f;
    private const float EffectDuration = 0.6f;

    private static readonly ResPath UnitsRsi = new("_Starlight/Arcade/Lancer/lancer_units.rsi");
    private static readonly ResPath EffectsRsi = new("_Starlight/Arcade/Lancer/lancer_effects.rsi");

    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private LancerGameStateSnapshot? _snapshot;
    private LancerGridCoord? _hover;

    private readonly List<(LancerGridCoord Cell, LancerAttackEffectKind Kind, LancerGridCoord? From, float Remaining)> _effects = new();

    public LancerGridCoord? HoveredCoord => _hover;

    public event Action<LancerGridCoord>? OnCellClicked;
    public event Action<LancerGridCoord?>? OnHexHovered;

    public LancerBoardControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        MinSize = ComputeBoardSize();
    }

    public void UpdateSnapshot(LancerGameStateSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public void PlayEffect(LancerGridCoord cell, LancerAttackEffectKind kind, LancerGridCoord? from)
    {
        _effects.Add((cell, kind, from, EffectDuration));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (_effects.Count == 0)
            return;

        for (var i = _effects.Count - 1; i >= 0; i--)
        {
            var e = _effects[i];
            e.Remaining -= args.DeltaSeconds;
            if (e.Remaining <= 0)
                _effects.RemoveAt(i);
            else
                _effects[i] = e;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var scale = UIScale;
        var hexSize = HexSize * scale;
        var origin = GetOrigin(hexSize);

        // Hex cells
        for (var y = 0; y < LancerHex.GridSize; y++)
        for (var x = 0; x < LancerHex.GridSize; x++)
        {
            var coord = new LancerGridCoord(x, y);
            var center = origin + LancerHex.HexToPixel(coord, hexSize);
            var corners = LancerHex.GetHexCorners(center, hexSize);

            var color = Color.FromHex("#2A2A35");
            if (_snapshot != null)
            {
                var cell = _snapshot.Cells[y][x];
                color = cell.Terrain switch
                {
                    LancerTerrainType.Relay => Color.FromHex("#3A4A5A"),
                    LancerTerrainType.RubbleSoft => Color.FromHex("#4A4035"),
                    LancerTerrainType.RubbleHard => Color.FromHex("#5A5040"),
                    _ => Color.FromHex("#2A2A35")
                };

                color = cell.Highlight switch
                {
                    LancerCellHighlight.Reachable => Color.FromHex("#2A4A2A"),
                    LancerCellHighlight.Target => Color.FromHex("#4A2A2A"),
                    LancerCellHighlight.Blast => Color.FromHex("#4A3A1A"),
                    _ => color
                };
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, corners, color);
            handle.DrawPrimitives(DrawPrimitiveTopology.LineLoop, corners, Color.FromHex("#111114"));
        }

        if (_hover is { } hover && LancerHex.InBounds(hover))
        {
            var center = origin + LancerHex.HexToPixel(hover, hexSize);
            handle.DrawPrimitives(DrawPrimitiveTopology.LineLoop, LancerHex.GetHexCorners(center, hexSize), Color.FromHex("#F0E080"));
        }

        foreach (var effect in _effects)
        {
            if (effect.From is not { } from)
                continue;

            var a = origin + LancerHex.HexToPixel(from, hexSize);
            var b = origin + LancerHex.HexToPixel(effect.Cell, hexSize);
            var alpha = Math.Clamp(effect.Remaining / EffectDuration, 0f, 1f);
            handle.DrawLine(a, b, Color.FromHex("#FFEE88").WithAlpha(alpha));
        }

        if (_snapshot != null)
        {
            foreach (var unit in _snapshot.Units)
            {
                var center = origin + LancerHex.HexToPixel(unit.Position, hexSize);
                var tex = TryGetUnitTexture(unit);
                if (tex != null)
                {
                    var size = new Vector2(hexSize * 0.85f, hexSize * 0.85f);
                    handle.DrawTextureRect(tex, UIBox2.FromDimensions(center - size / 2f, size));
                }

                if (unit.MaxHp > 0 && unit.Hp < unit.MaxHp)
                {
                    var barWidth = hexSize * 0.7f;
                    var barPos = center + new Vector2(-barWidth / 2f, hexSize * 0.35f);
                    handle.DrawRect(UIBox2.FromDimensions(barPos, new Vector2(barWidth, 3f * scale)), Color.FromHex("#220000"));
                    var ratio = Math.Clamp(unit.Hp / (float) unit.MaxHp, 0f, 1f);
                    handle.DrawRect(UIBox2.FromDimensions(barPos, new Vector2(barWidth * ratio, 3f * scale)), Color.FromHex("#44CC44"));
                }
            }
        }

        foreach (var effect in _effects)
        {
            var tex = TryGetEffectTexture(effect.Kind);
            if (tex == null)
                continue;

            var center = origin + LancerHex.HexToPixel(effect.Cell, hexSize);
            var size = new Vector2(hexSize, hexSize);
            var alpha = Math.Clamp(effect.Remaining / EffectDuration, 0f, 1f);
            handle.DrawTextureRect(tex, UIBox2.FromDimensions(center - size / 2f, size), Color.White.WithAlpha(alpha));
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var coord = PixelToCoord(args.RelativePixelPosition);
        if (coord == null)
            return;

        args.Handle();
        OnCellClicked?.Invoke(coord);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        SetHover(PixelToCoord(args.RelativePixelPosition));
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        SetHover(null);
    }

    private void SetHover(LancerGridCoord? coord)
    {
        if (_hover != null && coord != null && _hover.X == coord.X && _hover.Y == coord.Y)
            return;

        if (_hover == null && coord == null)
            return;

        _hover = coord;
        OnHexHovered?.Invoke(coord);
    }

    private LancerGridCoord? PixelToCoord(Vector2 relativePixel)
    {
        var hexSize = HexSize * UIScale;
        var origin = GetOrigin(hexSize);
        var local = relativePixel - origin;
        var hex = LancerHex.PixelToHex(local, hexSize);
        return LancerHex.InBounds(hex) ? hex : null;
    }

    private Vector2 GetOrigin(float hexSize)
    {
        var outer = hexSize / MathF.Sqrt(3f);
        return new Vector2(outer + 4f * UIScale, outer + 4f * UIScale);
    }

    private static Vector2 ComputeBoardSize()
    {
        var last = LancerHex.HexToPixel(new LancerGridCoord(LancerHex.GridSize - 1, LancerHex.GridSize - 1), HexSize);
        var outer = HexSize / MathF.Sqrt(3f);
        // UI units (unscaled); Draw applies UIScale separately.
        return new Vector2(last.X + outer * 2f + 8f, last.Y + outer * 2f + 8f);
    }

    /// <summary>
    /// Pixel-space bounds of the hex grid within this control (for dice overlay clamping).
    /// </summary>
    public UIBox2 GetPlayAreaPixelBounds()
    {
        var scale = UIScale;
        var hexSize = HexSize * scale;
        var origin = GetOrigin(hexSize);
        var last = LancerHex.HexToPixel(new LancerGridCoord(LancerHex.GridSize - 1, LancerHex.GridSize - 1), hexSize);
        var outer = hexSize / MathF.Sqrt(3f);
        var bottomRight = last + new Vector2(outer + 4f * scale, outer + 4f * scale);
        return new UIBox2(origin, bottomRight);
    }

    private Texture? TryGetUnitTexture(LancerUnitState unit)
    {
        var state = !string.IsNullOrEmpty(unit.SpriteState)
            ? unit.SpriteState
            : unit.Kind switch
            {
                LancerUnitKind.PlayerMech => "everest_blue",
                LancerUnitKind.Grunt => "urbie",
                LancerUnitKind.Urbie => "urbie",
                LancerUnitKind.Assault => "kerberos_grunt",
                LancerUnitKind.Cutlass => "kerberos_archer",
                LancerUnitKind.Sniper => "kerberos_sniper",
                LancerUnitKind.Bombard => "kerberos_bombard",
                _ => null
            };

        return state == null ? null : TryGetRsiFrame(UnitsRsi, state);
    }

    private Texture? TryGetEffectTexture(LancerAttackEffectKind kind)
    {
        var state = kind switch
        {
            LancerAttackEffectKind.RifleFlash => "rifle_flash",
            LancerAttackEffectKind.AmrImpact => "amr_impact",
            LancerAttackEffectKind.KnifeSlash => "knife_slash",
            LancerAttackEffectKind.HexBlast => "hex_blast",
            LancerAttackEffectKind.RocketImpact => "amr_impact",
            LancerAttackEffectKind.MissileBlast => "hex_blast",
            _ => null
        };

        return state == null ? null : TryGetRsiFrame(EffectsRsi, state);
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
