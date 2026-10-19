using System.Numerics;
using Content.Shared._Starlight.DirectionalSigns.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.DirectionalSigns;

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
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private SpriteSystem _sprite = default!;

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

        // Starlight begin
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

        foreach (var gridUid in _staleGrids)
        {
            _gridEyeRotations.Remove(gridUid);
        }
        _staleGrids.Clear();

        if (_rotatedGrids.Count > 0)
        {
            var query = EntityQueryEnumerator<DirectionalSignComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var signXform))
            {
                if (signXform.GridUid is { } gridUid && _rotatedGrids.Contains(gridUid))
                    _dirtySigns.Add(uid);
            }

            foreach (var gridUid in _rotatedGrids)
            {
                if (TryComp(gridUid, out TransformComponent? gridXform))
                    _gridEyeRotations[gridUid] = GetAdjustedEyeRotation(baseEyeRotationDeg, gridXform);
            }
            _rotatedGrids.Clear();
        }

        foreach (var uid in _dirtySigns)
        {
            UpdateSign(uid, baseEyeRotationDeg);
        }
        _dirtySigns.Clear();
        // Starlight end
    }
}
