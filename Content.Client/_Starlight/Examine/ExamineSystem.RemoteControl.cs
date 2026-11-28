// ReSharper disable CheckNamespace
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.Examine;

public sealed partial class ExamineSystem
{
    private bool TryGetRemoteTooltipPosition(
        EntityUid target,
        EntityUid? examiner,
        bool centeredOnCursor,
        out ScreenCoordinates position)
    {
        position = default;
        if (examiner is not { } remoteEntity
            || _remoteControl.ControlledEntity != remoteEntity
            || _remoteControl.ControlledViewport is not { } viewport
            || viewport.Window is not { } window
            || _remoteControl.ControlledEye is not { } eye)
            return false;

        var mapPosition = centeredOnCursor && _remoteControl.RemoteMousePosition is { } remoteMousePosition
            ? remoteMousePosition
            : EntityManager.System<SharedTransformSystem>().GetMapCoordinates(target);
        if (mapPosition.MapId != eye.Position.MapId)
            return false;

        position = _userInterfaceManager.ScreenToUIPosition(
            new ScreenCoordinates(viewport.WorldToScreen(mapPosition.Position), window.Id));
        return true;
    }
}
