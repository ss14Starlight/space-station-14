using System.Numerics;
using Content.Shared._Starlight.Xenoborgs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Overlay.Overlays;

/// <summary>
/// Draws a screen-space direction arrow pointing toward the IntrinsicPinpointer's target.
/// The arrow appears in the bottom-left corner of the game viewport.
/// Angle and distance are computed client-side from the world position stored in
/// <see cref="IntrinsicPinpointerComponent.TargetWorldPos"/> — no server angle math needed.
/// </summary>
public sealed class IntrinsicPinpointerOverlay : Robust.Client.Graphics.Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly IEyeManager _eye;
    private readonly SharedTransformSystem _xform;

    private readonly Texture _arrowDirect;   // pointing direction
    private readonly Texture _arrowFar;
    private readonly Texture _arrowMedium;
    private readonly Texture _arrowClose;
    private readonly Texture _arrowNull;     // no signal

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public IntrinsicPinpointerOverlay(IEntityManager entMan, IPlayerManager player, IEyeManager eye)
    {
        _entMan = entMan;
        _player = player;
        _eye = eye;
        _xform = entMan.EntitySysManager.GetEntitySystem<SharedTransformSystem>();

        var sprites = entMan.EntitySysManager.GetEntitySystem<SpriteSystem>();
        var rsi = new ResPath("/Textures/Objects/Devices/pinpointer.rsi");

        _arrowDirect = sprites.Frame0(new SpriteSpecifier.Rsi(rsi, "pinondirect"));
        _arrowFar    = sprites.Frame0(new SpriteSpecifier.Rsi(rsi, "pinonfar"));
        _arrowMedium = sprites.Frame0(new SpriteSpecifier.Rsi(rsi, "pinonmedium"));
        _arrowClose  = sprites.Frame0(new SpriteSpecifier.Rsi(rsi, "pinonclose"));
        _arrowNull   = sprites.Frame0(new SpriteSpecifier.Rsi(rsi, "pinonnull"));
    }

    // Always return true — the check inside Draw() is cheaper and more reliable.
    protected override bool BeforeDraw(in OverlayDrawArgs args) => true;

    protected override void Draw(in OverlayDrawArgs args)
    {
        // Only draw for the local player when they have the component.
        var uid = _player.LocalEntity;
        if (uid == null)
            return;

        if (!_entMan.TryGetComponent(uid.Value, out IntrinsicPinpointerComponent? comp))
            return;

        var screen = args.ScreenHandle;
        var bounds = args.ViewportBounds;

        // Use viewport dimensions (not global position) for correct local-coordinate placement.
        const float Size = 96f;
        const float Margin = 16f;
        var center = new Vector2(
            Margin + Size * 0.5f,
            bounds.Height - Margin - Size * 0.5f);

        // Draw dark semi-transparent background circle so the arrow is readable against any map.
        DrawBackground(screen, center, Size);

        // No target → show the null/no-signal icon, no rotation.
        if (comp.TargetWorldPos is not { } targetWorldPos)
        {
            DrawArrow(screen, _arrowNull, center, Size, Angle.Zero);
            return;
        }

        // Get the player's current world position for direction calculation.
        if (!_entMan.TryGetComponent(uid.Value, out TransformComponent? xform))
        {
            DrawArrow(screen, _arrowNull, center, Size, Angle.Zero);
            return;
        }

        // Target on different map → no meaningful direction, show null icon.
        if (comp.TargetMapId.HasValue && xform.MapID != comp.TargetMapId.Value)
        {
            DrawArrow(screen, _arrowNull, center, Size, Angle.Zero);
            return;
        }

        var playerPos = _xform.GetWorldPosition(xform);
        var dir = targetWorldPos - playerPos;
        var dist = dir.Length();

        // World angle → screen angle conversion.
        // Texture: arrowhead at bottom of image → tip at local (0,+half) → after SetTransform θ:
        //   tip direction = (-sin(θ), cos(θ)).
        // World: Y-UP (south=(0,-1) per Angle.cs). North=(0,+1) → ToWorldAngle=π, East=(1,0) → π/2.
        // Screen: want tip pointing toward target. Solving (-sin(θ),cos(θ))=(sin(A),-cos(A)) (world sprite formula)
        //   gives θ = -A. With camera: screenAngle = -worldAngle + eyeAngle.
        var worldAngle = dir.ToWorldAngle();
        var eyeAngle   = _eye.CurrentEye?.Rotation ?? Angle.Zero;
        var screenAngle = new Angle(-worldAngle.Theta + eyeAngle.Theta);

        DrawArrow(screen, PickTexture(dist, comp), center, Size, screenAngle);
    }

    private Texture PickTexture(float dist, IntrinsicPinpointerComponent comp)
    {
        if (dist <= comp.ReachedDistance) return _arrowClose;
        if (dist <= comp.CloseDistance)   return _arrowClose;
        if (dist <= comp.MediumDistance)  return _arrowMedium;
        return _arrowFar;
    }

    private static void DrawBackground(DrawingHandleScreen screen, Vector2 center, float size)
    {
        var radius = size * 0.55f;
        screen.DrawCircle(center, radius, new Color(0f, 0f, 0f, 0.55f));
    }

    private static void DrawArrow(DrawingHandleScreen screen, Texture tex,
        Vector2 center, float size, Angle screenAngle)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        var dest = UIBox2.FromDimensions(-half, new Vector2(size, size));

        screen.SetTransform(center, screenAngle);
        screen.DrawTextureRect(tex, dest);
        screen.SetTransform(Matrix3x2.Identity);
    }
}
