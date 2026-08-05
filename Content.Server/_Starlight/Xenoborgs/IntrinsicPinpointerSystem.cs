using Content.Shared._Starlight.Xenoborgs.Components;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Xenoborgs;

/// <summary>
/// Server-side: each tick, finds the nearest entity with the target component,
/// updates <see cref="IntrinsicPinpointerComponent.TargetWorldPos"/> and
/// <see cref="IntrinsicPinpointerComponent.TargetMapId"/> so the client
/// overlay can draw a HUD arrow.
/// </summary>
public sealed partial class IntrinsicPinpointerSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<IntrinsicPinpointerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            UpdatePinpointer((uid, comp, xform));
        }
    }

    private void UpdatePinpointer(Entity<IntrinsicPinpointerComponent, TransformComponent> ent)
    {
        var (uid, comp, xform) = ent;

        if (comp.Component == null)
            return;

        if (!EntityManager.ComponentFactory.TryGetRegistration(comp.Component, out var reg))
            return;

        var target = FindNearest(uid, xform, reg.Type);
        if (target == null)
        {
            if (comp.TargetWorldPos.HasValue || comp.TargetMapId.HasValue)
            {
                comp.TargetWorldPos = null;
                comp.TargetMapId    = null;
                Dirty(uid, comp);
            }
            return;
        }

        if (!_xformQuery.TryGetComponent(target.Value, out var targetXform))
            return;

        var targetPos   = _transform.GetWorldPosition(targetXform);
        var targetMapId = targetXform.MapID;

        if (comp.TargetWorldPos != targetPos || comp.TargetMapId != targetMapId)
        {
            comp.TargetWorldPos = targetPos;
            comp.TargetMapId    = targetMapId;
            Dirty(uid, comp);
        }
    }

    private EntityUid? FindNearest(EntityUid uid, TransformComponent xform, Type componentType)
    {
        var selfPos = _transform.GetWorldPosition(xform);
        var selfMap = xform.MapID;
        EntityUid? best        = null;
        EntityUid? bestCross   = null;
        var bestDist      = float.MaxValue;
        var bestDistCross = float.MaxValue;

        foreach (var (otherUid, _) in EntityManager.GetAllComponents(componentType))
        {
            if (otherUid == uid)
                continue;
            if (!_xformQuery.TryGetComponent(otherUid, out var otherXform))
                continue;

            var dist = (_transform.GetWorldPosition(otherXform) - selfPos).LengthSquared();
            if (otherXform.MapID == selfMap)
            {
                // Prefer same-map targets
                if (dist < bestDist) { bestDist = dist; best = otherUid; }
            }
            else if (otherXform.MapID != MapId.Nullspace)
            {
                // Fall back to cross-map if nothing on same map
                if (dist < bestDistCross) { bestDistCross = dist; bestCross = otherUid; }
            }
        }

        return best ?? bestCross;
    }
}
