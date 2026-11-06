using System.Numerics;
using Content.Shared._Starlight.DirectionalSigns.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Starlight.DirectionalSigns;

/// <summary>
/// Maintains the orientation of directional signs
/// </summary>
/// <remarks>
/// Maintains the orientation of directional signs relative to the player's 'eye'
/// while keeping directional signs snapped to GRID-space cardinal directions.
/// Keep changes to client-side sprite to avoid unnecessary network traffic.
/// </remarks>
public sealed partial class DirectionalSignSystem : EntitySystem
{
    [Dependency] private IEyeManager _eyeManager = null!;
    [Dependency] private SpriteSystem _sprite = null!;

    private const float EpsilonDeg = 0.001f;

    private readonly HashSet<EntityUid> _dirtySigns = [];
    private readonly Dictionary<EntityUid, float> _gridEyeRotations = [];
    private readonly HashSet<EntityUid> _rotatedGrids = [];
    private readonly List<EntityUid> _staleGrids = [];

    [SubscribeLocalEvent]
    private void OnStartup(Entity<DirectionalSignComponent> ent, ref ComponentStartup args) => _dirtySigns.Add(ent);

    [SubscribeLocalEvent]
    private void OnMove(Entity<DirectionalSignComponent> ent, ref MoveEvent args) =>
        _dirtySigns.Add(ent);

    private static float GetAdjustedEyeRotation(float baseEyeRotationDeg, TransformComponent gridXform) =>
        baseEyeRotationDeg + (float)gridXform.LocalRotation.Reduced().Degrees;

    public override void FrameUpdate(float frameTime)
    {
        // Get current eye rotation
        // Must be made relative to GRID-space inside the while loop as some signs
        // will be on a different grid than the player's controlled entity
        var eye = _eyeManager.CurrentEye;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (eye is null)
            return;
        var baseEyeRotationDeg = (float)eye.Rotation.Reduced().Degrees;

        foreach (var (gridUid, lastRotationDeg) in _gridEyeRotations)
        {
            if (!TryComp(gridUid, out TransformComponent? gridXform))
            {
                _staleGrids.Add(gridUid);
                continue;
            }

            if (MathF.Abs(GetAdjustedEyeRotation(baseEyeRotationDeg, gridXform) - lastRotationDeg) >= EpsilonDeg)
                _rotatedGrids.Add(gridUid);
        }

        foreach (var gridUid in _staleGrids) _gridEyeRotations.Remove(gridUid);
        _staleGrids.Clear();

        if (_rotatedGrids.Count > 0)
        {
            var query = EntityQueryEnumerator<DirectionalSignComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var signXform))
                if (signXform.GridUid is { } gridUid && _rotatedGrids.Contains(gridUid))
                    _dirtySigns.Add(uid);

            foreach (var gridUid in _rotatedGrids)
                if (TryComp(gridUid, out TransformComponent? gridXform))
                    _gridEyeRotations[gridUid] = GetAdjustedEyeRotation(baseEyeRotationDeg, gridXform);
            _rotatedGrids.Clear();
        }

        foreach (var uid in _dirtySigns) UpdateSign(uid, baseEyeRotationDeg);
        _dirtySigns.Clear();
    }

    private void UpdateSign(EntityUid uid, float baseEyeRotationDeg)
    {
        if (!TryComp(uid, out TransformComponent? signXform) || !TryComp(uid, out SpriteComponent? sprite))
            return;

        // Escape early if the sign isn't on a grid, or its grid can't be resolved.
        // It'll be marked dirty again when it moves onto one.
        var gridUid = signXform.GridUid;
        if (gridUid is null || gridUid.Value == EntityUid.Invalid)
            return;

        // Correct eyeRotation for gridRotation
        if (!TryComp(gridUid.Value, out TransformComponent? gridXform))
            return;
        var adjustedEyeRotationDeg = GetAdjustedEyeRotation(baseEyeRotationDeg, gridXform);
        _gridEyeRotations.TryAdd(gridUid.Value, adjustedEyeRotationDeg);

        // Force NoRotation, we will control rotation manually in this system
        if (!sprite.NoRotation)
            sprite.NoRotation = true;

        // Define initial positions
        var spriteCenter = signXform.LocalPosition;
        var tileCenter = new Vector2(
            MathF.Floor(spriteCenter.X) + 0.5f,
            MathF.Floor(spriteCenter.Y) + 0.5f);
        var spriteTileCenterOffset = spriteCenter - tileCenter;

        // Define caseID based on eyeRotation: 0deg>0, 90deg>1, 180deg>2, 270deg>3
        var caseID = (((int)MathF.Floor((adjustedEyeRotationDeg + 45) / 90) % 4) + 4) % 4;
        var nearestCardinalDeg = caseID * 90;

        // Smoothly counter-rotate within the quadrant; jump at 45° because 'nearestCardinalDeg' flips.
        // This makes the sprites snap to GRID-space cardinals instead of following eye rotation
        // while the eye rotation animation is in progress
        var eyeDeltaNearestCardinalRad = Angle.FromDegrees(adjustedEyeRotationDeg - nearestCardinalDeg);

        // Calculate the sprite offset vector in GRID-space
        var newSpriteTileCenterOffset = caseID switch
        {
            0 => new Vector2( spriteTileCenterOffset.X,  spriteTileCenterOffset.Y),   // 0°
            1 => new Vector2( spriteTileCenterOffset.Y, -spriteTileCenterOffset.X),   // 90°
            2 => new Vector2(-spriteTileCenterOffset.X, -spriteTileCenterOffset.Y),   // 180°
            3 => new Vector2(-spriteTileCenterOffset.Y,  spriteTileCenterOffset.X),   // 270°
            _ => Vector2.Zero,
        };

        // GRID-space vector from current sprite center to desired sprite center.
        var spriteOffsetGrid = newSpriteTileCenterOffset - spriteTileCenterOffset;
        // Convert GRID-space vector into CAMERA-space vector
        var spriteOffsetCamera = Angle.FromDegrees(adjustedEyeRotationDeg).RotateVec(spriteOffsetGrid);

        // Push the values to the sprite
        _sprite.SetRotation((uid, sprite), eyeDeltaNearestCardinalRad);
        _sprite.SetOffset((uid, sprite), spriteOffsetCamera);
    }
}
