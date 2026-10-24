using System.Numerics;
using Content.Client._Starlight.Audio;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Atmos;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;

namespace Content.Client._Starlight.MindCollective;

public sealed partial class MindCollectiveObserverSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private VacuumHearingSystem _vacuum = default!;

    private readonly Robust.Shared.Graphics.Eye _camera = new();
    private readonly record struct Listener(EntityUid Body, MapCoordinates Coordinates, float Pressure, bool SealedHelmet);
    private Listener[] _listeners = [];
    private bool _listening;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(EyeSystem));
        UpdatesBefore.Add(typeof(AudioSystem));
    }

    public override void Shutdown()
    {
        if (_listening)
            _audio.ProcessStreamOverride -= ProcessCollectiveSound;
        RestoreCamera();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var listening = TryComp<MindCollectiveObserverComponent>(_player.LocalEntity, out var observer);
        if (listening != _listening)
        {
            _listening = listening;
            if (listening)
                _audio.ProcessStreamOverride += ProcessCollectiveSound;
            else
                _audio.ProcessStreamOverride -= ProcessCollectiveSound;
        }
        if (!listening)
        {
            _listeners = [];
            RestoreCamera();
            return;
        }

        var positions = new List<Listener>();
        foreach (var body in observer!.Bodies)
        {
            if (TryComp(body, out TransformComponent? xform))
            {
                var hearing = new MindCollectiveHearingState(GetNetEntity(body), Atmospherics.OneAtmosphere, false);
                foreach (var state in observer.Hearing)
                {
                    if (state.Body != hearing.Body)
                        continue;
                    hearing = state;
                    break;
                }
                positions.Add(new Listener(body, _transform.GetMapCoordinates(body, xform: xform),
                    hearing.Pressure, hearing.SealedHelmet));
            }
        }
        _listeners = positions.ToArray();
        if (!TryComp<EyeComponent>(observer.Viewing, out var viewed))
        {
            RestoreCamera();
            return;
        }
        _camera.Position = viewed.Eye.Position;
        _camera.Zoom = viewed.Eye.Zoom;
        _camera.Rotation = viewed.Eye.Rotation;
        _camera.Offset = viewed.Eye.Offset;
        _camera.DrawFov = viewed.Eye.DrawFov;
        _camera.DrawLight = viewed.Eye.DrawLight;
        _eyeManager.CurrentEye = _camera;
    }

    private void RestoreCamera()
    {
        if (!ReferenceEquals(_eyeManager.CurrentEye, _camera))
            return;
        if (TryComp<EyeComponent>(_player.LocalEntity, out var eye))
            _eyeManager.CurrentEye = eye.Eye;
        else
            _eyeManager.ClearCurrentEye();
    }

    // Play each sound once, relative to the collective body that hears it most clearly.
    private void ProcessCollectiveSound(EntityUid uid, AudioComponent audio, TransformComponent xform, MapCoordinates camera)
    {
        if (!audio.Started)
        {
            audio.Started = true;
            audio.StartPlaying();
        }
        if (audio.Global)
        {
            if (xform.MapID != MapId.Nullspace && !Array.Exists(_listeners, point => point.Coordinates.MapId == xform.MapID))
                audio.Gain = 0f;
            else
                audio.Volume = audio.Params.Volume;
            return;
        }

        var position = (audio.Flags & AudioFlags.GridAudio) != 0
            ? _maps.GetGridPosition(xform.ParentUid)
            : _transform.GetWorldPosition(xform);
        var bestAudibility = -1f;
        var bestOcclusion = 0f;
        var maxDistance = audio.MaxDistance;
        var referenceDistance = _audio.GetAudioDistance(audio.Params.ReferenceDistance);
        Listener? listener = null;
        foreach (var point in _listeners)
        {
            if (point.Coordinates.MapId != xform.MapID)
                continue;
            var offset = position - point.Coordinates.Position;
            var range = offset.Length();
            var audioDistance = _audio.GetAudioDistance(range);
            if (audioDistance > maxDistance)
                continue;
            var occlusion = (audio.Flags & AudioFlags.NoOcclusion) != 0
                ? 0f
                : _vacuum.GetOcclusionForListener(point.Body, point.Pressure, point.SealedHelmet,
                    point.Coordinates, offset, range, xform.ParentUid);
            // Estimate the default linear attenuation and the engine's occlusion gain.
            var attenuation = Math.Clamp(1f - audio.Params.RolloffFactor * MathF.Max(0f, audioDistance - referenceDistance)
                / MathF.Max(0.01f, maxDistance - referenceDistance), 0f, 1f);
            var audibility = attenuation * MathF.Exp(-occlusion * 0.1f);
            if (audibility <= bestAudibility)
                continue;
            bestAudibility = audibility;
            bestOcclusion = occlusion;
            listener = point;
        }
        if (listener is not { } best)
        {
            audio.Gain = 0f;
            return;
        }

        audio.Volume = audio.Params.Volume;
        var delta = position - best.Coordinates.Position;
        audio.Occlusion = bestOcclusion;
        audio.Position = camera.Position + delta;
        audio.Velocity = Vector2.Zero;
    }
}
