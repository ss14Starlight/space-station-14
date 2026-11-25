using Content.Shared._FarHorizons.DirectionalOverride;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.DirectionalOverride;

public sealed partial class DirectionalOverrideSystem: EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var eyeRotation = _eye.CurrentEye.Rotation;

        var query = EntityQueryEnumerator<DirectionalOverrideComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var doComp, out var sprite, out var xform))
        {
            if (_timing.CurTime < doComp.NextUpdate)
                continue;
            doComp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.1);

            var direction = (_transform.GetWorldRotation(xform) + eyeRotation).GetCardinalDir();

            if (doComp.Offsets.TryGetValue(direction, out var offset))
                _sprite.SetOffset((uid, sprite), offset);

            if (doComp.DrawDepths.TryGetValue(direction, out var depth))
                _sprite.SetDrawDepth((uid, sprite), (int) depth);
        }
    }
}
