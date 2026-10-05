using Content.Shared.MouseRotator;
using Content.Client._Starlight.Computers.RemoteControl;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.MouseRotator;

/// <inheritdoc/>
public sealed partial class MouseRotatorSystem : SharedMouseRotatorSystem
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    private RemoteControlInterface _remoteControl => EntityManager.System<RemoteControlInterface>(); // Starlight

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted || !_input.MouseScreenPosition.IsValid)
            return;

        var player = _player.LocalEntity;

        if (player == null)
            return;

        // Starlight-start: use the controlled entity and its viewport while remotely controlling.
        var rotationEntity = player.Value;
        IEye eye = _eye.CurrentEye;
        var useRemoteRotator = false;
        if (_remoteControl.ControlledEntity is { } controlled
            && _remoteControl.ControlledEye is { } controlledEye
            && TryComp<MouseRotatorComponent>(controlled, out var remoteRotator))
        {
            rotationEntity = controlled;
            eye = controlledEye;
            useRemoteRotator = true;
        }

        if (!TryComp<MouseRotatorComponent>(rotationEntity, out var rotator))
            return;

        var xform = Transform(rotationEntity);

        // Get mouse loc and convert to angle based on player location
        var coords = _input.MouseScreenPosition;
        MapCoordinates? mapPos;
        if (useRemoteRotator)
            mapPos = _remoteControl.RemoteMousePosition;
        else
            mapPos = _eye.PixelToMap(coords);
        // Starlight-end

        if (mapPos is not { } remoteMapPosition)
            return;

        if (remoteMapPosition.MapId == MapId.Nullspace)
            return;

        var angle = (remoteMapPosition.Position - _transform.GetMapCoordinates(rotationEntity, xform: xform).Position).ToWorldAngle();

        var curRot = _transform.GetWorldRotation(xform);

        // 4-dir handling is separate --
        // only raise event if the cardinal direction has changed
        if (rotator.Simple4DirMode)
        {
            var eyeRot = eye.Rotation; // camera rotation
            var angleDir = (angle + eyeRot).GetCardinalDir(); // apply GetCardinalDir in the camera frame, not in the world frame
            if (angleDir == (curRot + eyeRot).GetCardinalDir())
                return;

            var rotation = angleDir.ToAngle() - eyeRot; // convert back to world frame
            if (rotation >= Math.PI) // convert to [-PI, +PI)
                rotation -= 2 * Math.PI;
            else if (rotation < -Math.PI)
                rotation += 2 * Math.PI;
            RaisePredictiveEvent(new RequestMouseRotatorRotationEvent
            {
                Rotation = rotation,
                User = GetNetEntity(player.Value)
            });

            return;
        }

        // Don't raise event if mouse ~hasn't moved (or if too close to goal rotation already)
        var diff = Angle.ShortestDistance(angle, curRot);
        if (Math.Abs(diff.Theta) < rotator.AngleTolerance.Theta)
            return;

        if (rotator.GoalRotation != null)
        {
            var goalDiff = Angle.ShortestDistance(angle, rotator.GoalRotation.Value);
            if (Math.Abs(goalDiff.Theta) < rotator.AngleTolerance.Theta)
                return;
        }

        RaisePredictiveEvent(new RequestMouseRotatorRotationEvent
        {
            Rotation = angle,
            User = GetNetEntity(player.Value)
        });
    }
}
