using System.Numerics;
using Content.Shared._Starlight.Antags.Vampires;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Antags.Vampires;

/// <summary>
/// Client-side system to render shadow boxing punch travel effects
/// </summary>
public sealed class VampireShadowBoxingSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private record struct ActivePunch(EntityUid Entity, EntityUid Source, EntityUid Target, TimeSpan SpawnTime, TimeSpan LifeTime);
    private readonly List<ActivePunch> _active = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<VampireShadowBoxingPunchEvent>(OnPunchEvent);
    }

    private void OnPunchEvent(VampireShadowBoxingPunchEvent ev)
    {
        var source = GetEntity(ev.Source);
        var target = GetEntity(ev.Target);
        if (!Exists(source) || !Exists(target))
            return;

        var srcPos = _transform.GetWorldPosition(source);

        var punch = Spawn(ev.EffectProto, Transform(source).Coordinates);
        var direction = _transform.GetWorldPosition(target) - srcPos;
        if (direction != Vector2.Zero)
        {
            // Set rotation so 4-directional RSI picks the correct direction frame
            var ang = direction.ToWorldAngle();
            _transform.SetWorldRotation(punch, ang);
        }
        _active.Add(new ActivePunch(punch, source, target, _timing.CurTime, ev.PunchLifetime));
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var ap = _active[i];
            if (!Exists(ap.Entity) || !Exists(ap.Source) || !Exists(ap.Target))
            {
                if (Exists(ap.Entity)) QueueDel(ap.Entity);
                _active.RemoveAt(i);
                continue;
            }
            var elapsed = now - ap.SpawnTime;
            if (elapsed > ap.LifeTime)
            {
                QueueDel(ap.Entity);
                _active.RemoveAt(i);
                continue;
            }
            var t = (float)(elapsed.TotalSeconds / ap.LifeTime.TotalSeconds);
            var start = _transform.GetWorldPosition(ap.Source);
            var end = _transform.GetWorldPosition(ap.Target);
            var pos = Vector2.Lerp(start, end, MathF.Min(1f, t));
            _transform.SetWorldPosition(ap.Entity, pos);
        }
    }
}
