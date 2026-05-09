using Content.Client._Starlight.Shaders;
using Content.Client._Starlight.Trail;
using Content.Shared.Starlight.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Overlay.Trail;

public sealed class TrailSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IStarlightShaderManager _shaderMan = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private TrailOverlay _overlay = default!;
    private bool _enabled = true;

    private const float TeleportThreshold = 3f;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new TrailOverlay(EntityManager, _shaderMan);
        _overlayMan.AddOverlay(_overlay);

        Subs.CVar(_cfg, StarlightCCVars.TracesEnabled, v =>
        {
            _enabled = v;
            _overlay.Enabled = v;
        }, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_enabled)
            return;

        var query = EntityQueryEnumerator<TrailComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trail, out var xform))
        {
            var worldPos = _xform.GetWorldPosition(xform);
            var points = trail.Points;

            // Ensure ring buffer matches configured capacity
            if (points.Capacity != trail.MaxPoints)
                points.Resize(trail.MaxPoints);

            var moved = false;

            if (points.Count > 0)
            {
                var last = points[^1];
                var dist = (worldPos - last).Length();

                if (dist > TeleportThreshold)
                {
                    points.Clear();
                    points.PushBack(worldPos);
                    trail.IdleTimer = 0f;
                    continue;
                }

                if (dist >= trail.MinDistance)
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

            if (!moved && points.Count > 1)
            {
                trail.IdleTimer += frameTime;

                if (trail.IdleTimer > trail.DecayDelay)
                {
                    trail.IdleTimer -= trail.DecayInterval;
                    points.PopFront();
                }
            }
        }
    }
}
