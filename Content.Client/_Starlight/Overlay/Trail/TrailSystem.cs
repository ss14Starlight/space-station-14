using System.Diagnostics;
using Content.Client._Starlight.Shaders;
using Content.Client._Starlight.Trail;
using Content.Shared._Starlight.Trail;
using Content.Shared.Starlight.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Overlay.Trail;

public sealed class TrailSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IStarlightShaderManager _shaderMan = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private TrailOverlay _overlay = default!;
    private bool _enabled = true;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new TrailOverlay(EntityManager, _shaderMan, _sprite);
        _overlayMan.AddOverlay(_overlay);

        Subs.CVar(_cfg, StarlightCCVars.TracesEnabled, v =>
        {
            _enabled = v;
            _overlay.Enabled = v;
        }, true);

        SubscribeLocalEvent<TrailComponent, MapInitEvent>(OnMapInit);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    public void OnMapInit(Entity<TrailComponent> ent, ref MapInitEvent args)
    {
        // Ensure ring buffer matches configured capacity
        SyncCapacity(ent.Comp);
    }

    private static void SyncCapacity(TrailComponent comp)
    {
        if (comp.Points.Capacity != comp.MaxPoints)
            comp.Points.Resize(comp.MaxPoints);
        if (comp.Samples.Capacity != comp.MaxPoints)
            comp.Samples.Resize(comp.MaxPoints);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_enabled)
            return;

        var query = EntityQueryEnumerator<TrailComponent, TransformComponent, EyeComponent>();
        while (query.MoveNext(out var uid, out var trail, out var xform, out var eye))
        {
            var worldPos = _xform.GetWorldPosition(xform);
            var worldRot = _xform.GetWorldRotation(xform);
            var sample = new TrailSample() { Position = worldPos, EyeRotation = eye.Rotation, Rotation = worldRot };
            var points = trail.Points;
            var samples = trail.Samples;
            SyncCapacity(trail);

            var moved = false;

            if (trail.Mode == TrailMode.Ribbon)
            {
                if (points.Count > 0)
                {
                    var last = points[^1];
                    var distSq = (worldPos - last).LengthSquared();

                    if (distSq > trail.TeleportThreshold * trail.TeleportThreshold)
                    {
                        points.Clear();
                        points.PushBack(worldPos);
                        trail.IdleTimer = 0f;
                        continue;
                    }

                    if (distSq >= trail.MinDistance * trail.MinDistance)
                    {
                        points.PushBack(worldPos);
                        moved = true;
                        trail.IdleTimer = 0f;
                    }
                }
                else
                {
                    points.PushBack(worldPos);
                    moved = true;
                    trail.IdleTimer = 0f;
                }
            }
            else
            {
                if (samples.Count > 0)
                {
                    var lastSample = samples[^1];
                    var sampleDistSq = (worldPos - lastSample.Position).LengthSquared();

                    if (sampleDistSq > trail.TeleportThreshold * trail.TeleportThreshold)
                    {
                        samples.Clear();
                        samples.PushBack(sample);
                        trail.IdleTimer = 0f;
                        continue;
                    }

                    if (sampleDistSq >= trail.MinDistance * trail.MinDistance)
                    {
                        samples.PushBack(sample);
                        moved = true;
                        trail.IdleTimer = 0f;
                    }
                }
                else
                {
                    samples.PushBack(sample);
                    moved = true;
                    trail.IdleTimer = 0f;
                }
            }

            var activeCount = trail.Mode == TrailMode.Ribbon ? points.Count : samples.Count;
            if (!moved && activeCount > 1)
            {
                trail.IdleTimer += frameTime;

                if (trail.IdleTimer > trail.DecayDelay)
                {
                    trail.IdleTimer -= trail.DecayInterval;
                    if (trail.Mode == TrailMode.Ribbon)
                        points.PopFront();
                    else
                        samples.PopFront();
                }
            }
        }
    }
}
