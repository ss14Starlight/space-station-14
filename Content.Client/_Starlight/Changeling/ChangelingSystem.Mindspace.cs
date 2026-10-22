using System.Numerics;
using Content.Shared._Starlight.Changeling;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private IPlayerManager _hivePlayer = default!;
    [Dependency] private AudioSystem _hiveAudio = default!;
    [Dependency] private SharedTransformSystem _hiveTransform = default!;
    [Dependency] private SharedMapSystem _hiveMaps = default!;
    [Dependency] private IEyeManager _hiveEyeManager = default!;
    private readonly Robust.Shared.Graphics.Eye _hiveCamera = new();
    private MapCoordinates[] _hiveListeners = [];
    private bool _hiveListening;

    private void InitializeMindspace()
    {
        UpdatesAfter.Add(typeof(EyeSystem));
        UpdatesBefore.Add(typeof(AudioSystem));
    }

    public override void Shutdown()
    {
        if (_hiveListening)
            _hiveAudio.ProcessStreamOverride -= ProcessHiveSound;
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_colonyInputChanged)
        {
            _colonyInputChanged = false;
            EntityManager.System<InputSystem>().SetEntityContextActive();
        }
        var listening = TryComp<ChangelingMindspaceComponent>(_hivePlayer.LocalEntity, out var observer);
        if (listening != _hiveListening)
        {
            _hiveListening = listening;
            if (listening)
                _hiveAudio.ProcessStreamOverride += ProcessHiveSound;
            else
                _hiveAudio.ProcessStreamOverride -= ProcessHiveSound;
        }
        if (!listening)
        {
            _hiveListeners = [];
            if (ReferenceEquals(_hiveEyeManager.CurrentEye, _hiveCamera))
            {
                if (TryComp<EyeComponent>(_hivePlayer.LocalEntity, out var ownEye))
                    _hiveEyeManager.CurrentEye = ownEye.Eye;
                else
                    _hiveEyeManager.ClearCurrentEye();
            }
            return;
        }
        var positions = new List<MapCoordinates>();
        foreach (var body in observer!.Bodies)
        {
            if (TryComp(body, out TransformComponent? xform))
                positions.Add(_hiveTransform.GetMapCoordinates(body, xform: xform));
        }
        _hiveListeners = positions.ToArray();
        if (TryComp<EyeComponent>(observer.Viewing, out var viewed))
        {
            _hiveCamera.Position = viewed.Eye.Position;
            _hiveCamera.Zoom = viewed.Eye.Zoom;
            _hiveCamera.Rotation = viewed.Eye.Rotation;
            _hiveCamera.Offset = viewed.Eye.Offset;
            _hiveCamera.DrawFov = viewed.Eye.DrawFov;
            _hiveCamera.DrawLight = viewed.Eye.DrawLight;
            _hiveEyeManager.CurrentEye = _hiveCamera;
        }
    }

    // The engine exposes a single stream override. Subscribe only while this player is in mindspace.
    private void ProcessHiveSound(EntityUid uid, AudioComponent audio, TransformComponent xform, MapCoordinates camera)
    {
        if (!audio.Started)
        {
            audio.Started = true;
            audio.StartPlaying();
        }
        if (audio.Global)
        {
            if (xform.MapID != MapId.Nullspace && !Array.Exists(_hiveListeners, p => p.MapId == xform.MapID))
                audio.Gain = 0f;
            else
                audio.Volume = audio.Params.Volume;
            return;
        }
        var position = (audio.Flags & AudioFlags.GridAudio) != 0
            ? _hiveMaps.GetGridPosition(xform.ParentUid)
            : _hiveTransform.GetWorldPosition(xform);
        var distance = float.MaxValue;
        MapCoordinates? listener = null;
        foreach (var point in _hiveListeners)
        {
            if (point.MapId != xform.MapID)
                continue;
            var range = Vector2.Distance(point.Position, position);
            if (range >= distance)
                continue;
            distance = range;
            listener = point;
        }
        if (listener is not { } nearest || _hiveAudio.GetAudioDistance(distance) > audio.MaxDistance)
        {
            audio.Gain = 0f;
            return;
        }
        audio.Volume = audio.Params.Volume;
        var delta = position - nearest.Position;
        audio.Occlusion = (audio.Flags & AudioFlags.NoOcclusion) != 0
            ? 0f
            : _hiveAudio.GetOcclusion(nearest, delta, distance, xform.ParentUid);
        // One playback per sound, positioned relative to the body hearing it rather than the selected camera.
        audio.Position = camera.Position + delta;
        audio.Velocity = Vector2.Zero;
    }
}
