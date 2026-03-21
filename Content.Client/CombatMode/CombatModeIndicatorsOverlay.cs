using System.Numerics;
using Content.Client.Hands.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;

#region Starlight
using Content.Shared._Starlight.CombatMode;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp.PixelFormats;
using Robust.Shared.Graphics.RSI;
using Content.Client.Weapons.Ranged.Systems;
#endregion

namespace Content.Client.CombatMode;

/// <summary>
///   This shows something like crosshairs for the combat mode next to the mouse cursor.
///   For weapons with the gun class, a crosshair of one type is displayed,
///   while for all other types of weapons and items in hand, as well as for an empty hand,
///   a crosshair of a different type is displayed. These crosshairs simply show the state of combat mode (on|off).
/// </summary>
public sealed class CombatModeIndicatorsOverlay : Overlay
{
    private readonly IInputManager _inputManager;
    private readonly IEntityManager _entMan;
    private readonly IEyeManager _eye;
    private readonly CombatModeSystem _combat;
    private readonly HandsSystem _hands = default!;
    private readonly IClyde _clyde = default!; // Starlight-edit

    #region Starlight
    private readonly SightPrototype? _gunSight;
    private readonly SightPrototype? _gunBoltSight;
    private readonly SightPrototype? _meleeSight;
    private readonly float? _scale;
    private readonly float? _offset;
    private readonly Color? _main;
    private readonly Color? _second;
    private readonly bool _rotation = true;
    #endregion

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CombatModeIndicatorsOverlay(IInputManager input, IEntityManager entMan, IPrototypeManager prototypes,
            IEyeManager eye, CombatModeSystem combatSys, HandsSystem hands, IClyde clyde, SightPrototype gunSight, SightPrototype meleeSight, float scale, float offset, Color main, Color second, bool rotation = true) // Starlight-edit
    {
        _inputManager = input;
        _entMan = entMan;
        _eye = eye;
        _combat = combatSys;
        _hands = hands;
        // Starlight-start: replace Texture to Proto
        _gunSight = gunSight;
        _meleeSight = meleeSight;
        _clyde = clyde;
        _scale = scale;
        _offset = offset;
        _main = main;
        _second = second;
        _rotation = rotation;

        if (_gunSight.BoltVariant != null)
            prototypes.TryIndex(_gunSight.BoltVariant, out _gunBoltSight);
        // Starlight-end
    }

    // Starlight-start: Make it lambda
    protected override bool BeforeDraw(in OverlayDrawArgs args) 
        => !_combat.IsInCombatMode() ? false : base.BeforeDraw(in args);
    // Starlight-end

    protected override void Draw(in OverlayDrawArgs args)
    {
        var mouseScreenPosition = _inputManager.MouseScreenPosition;
        var mousePosMap = _eye.PixelToMap(mouseScreenPosition);
        if (mousePosMap.MapId != args.MapId)
            return;

        var handEntity = _hands.GetActiveHandEntity();
        var isHandGunItem = _entMan.TryGetComponent<GunComponent>(handEntity, out var gun); // Starlight-edit
        var isGunBolted = true;
        if (_entMan.TryGetComponent(handEntity, out ChamberMagazineAmmoProviderComponent? chamber))
            isGunBolted = chamber.BoltClosed ?? true;

        var mousePos = mouseScreenPosition.Position;
        var uiScale = (args.ViewportControl as Control)?.UIScale ?? 1f;
        var limitedScale = uiScale > 1.25f ? 1.25f : uiScale;

        #region Starlight

        var spriteSys = _entMan.EntitySysManager.GetEntitySystem<SpriteSystem>();

        var sight = isHandGunItem ? (isGunBolted || _gunBoltSight == null ? _gunSight : _gunBoltSight) : _meleeSight;
        if (sight != null)
        {
            if (!sight.ShowCursor)
                _clyde.SetCursor(_clyde.CreateCursor(new SixLabors.ImageSharp.Image<Rgba32>(32, 32), Vector2i.Zero));
            else
                _clyde.SetCursor(null);

            var scale = limitedScale * Math.Clamp(_scale ?? 0.6f, 0f, 1f);

            var eyePos = _eye.CurrentEye.Position;
            var eyeScreen = _eye.MapToScreen(eyePos);
            var rot = MathF.Atan2(eyeScreen.Y - mousePos.Y, eyeScreen.X - mousePos.X);
            rot -= MathF.PI / 2f;
            var rsiState = spriteSys.RsiStateLike(sight.Sprite);
            if (rsiState.IsAnimated && rsiState.AnimationFrameCount >= 3)
            {

                var currentAngle = 0f;
                if (isHandGunItem && handEntity != null)
                    currentAngle = (float)_entMan.System<GunSystem>().GetCurrentAngle(handEntity.Value).Degrees;

                var bracket1 = rsiState.GetFrame(RsiDirection.South, 0); // Left
                var sightTexture = rsiState.GetFrame(RsiDirection.South, 1); // Center
                var bracket2 = rsiState.GetFrame(RsiDirection.South, 2); // Right

                Texture? bracket3 = null; // Down
                Texture? bracket4 = null; // Up

                if (rsiState.AnimationFrameCount >= 4)
                    bracket3 = rsiState.GetFrame(RsiDirection.South, 3);

                if (rsiState.AnimationFrameCount >= 5)
                    bracket4 = rsiState.GetFrame(RsiDirection.South, 4);

                var offset = CalculateOffset(currentAngle, (eyeScreen.Position - mousePos).Length(), scale);
                DrawSightPartial(sightTexture, bracket1, bracket2, args.ScreenHandle, rot, mousePos, scale,_main ?? sight.MainColor,_second ?? sight.StrokeColor, offset, bracket3, bracket4);
            }
            else
            {
                var sightTexture = spriteSys.Frame0(sight.Sprite);
                DrawOverlayPart(sightTexture, args.ScreenHandle, mousePos, scale, _main ?? sight.MainColor, _second ?? sight.StrokeColor);
            }
        }

        #endregion
    }

    #region Starlight
    private float CalculateOffset(float currentAngle, float distance, float scale)
    {
        var angleRad = currentAngle * MathF.PI / 180f;
        var offset = MathF.Tan(angleRad) * distance;
        offset *= scale;
        // So if slider is in center: 0.5 + 0.5 = 1, meaning no change. If slider is at minimum: 0 + 0.5 = 0.5, meaning offset is halved. If slider is at maximum: 1 + 0.5 = 1.5, meaning offset is increased by 50%.
        offset *= (_offset ?? 0.5f) + 0.5f;
        return offset;
    }

    private void DrawSightPartial(Texture sight, Texture bracket1, Texture bracket2, DrawingHandleScreen screen, float rotation, Vector2 centerPos, float scale, Color mainColor, Color strokeColor, float offset, Texture? bracket3 = null, Texture? bracket4 = null)
    {
        DrawOverlayPart(sight, screen, centerPos, scale, mainColor, strokeColor);

        var bracketSize1 = bracket1.Size * scale;
        var bracketSize2 = bracket2.Size * scale;

        if (_rotation)
            screen.SetTransform(Matrix3x2.CreateRotation(rotation, centerPos));

        var bracket1Pos = centerPos + new Vector2(-offset - bracketSize1.X * 0.5f, 0f);
        var bracket2Pos = centerPos + new Vector2(offset + bracketSize2.X * 0.5f, 0f);
        DrawOverlayPart(bracket1, screen, bracket1Pos, scale, mainColor, strokeColor);
        DrawOverlayPart(bracket2, screen, bracket2Pos, scale, mainColor, strokeColor);

        if (bracket3 != null)
        {
            var bracket3Pos = centerPos + new Vector2(0f, offset + bracketSize1.Y * 0.5f);
            DrawOverlayPart(bracket3, screen, bracket3Pos, scale, mainColor, strokeColor);
        }

        if (bracket4 != null)
        {
            var bracket4Pos = centerPos + new Vector2(0f, -offset - bracketSize2.Y * 0.5f);
            DrawOverlayPart(bracket4, screen, bracket4Pos, scale, mainColor, strokeColor);
        }
    }

    private static void DrawOverlayPart(Texture texture, DrawingHandleScreen screen, Vector2 position, float scale, Color mainColor, Color strokeColor)
    {
        var size = texture.Size * scale;
        screen.DrawTextureRect(texture,
            UIBox2.FromDimensions(position - size * 0.5f, size), strokeColor);
        screen.DrawTextureRect(texture,
            UIBox2.FromDimensions(position - size * 0.5f - new Vector2(3f, 3f), size + new Vector2(7f, 7f)), mainColor);
    }

    private sealed class DummyCursor : ICursor
    {
        public void Dispose()
        {
        }
    }

    #endregion
}
