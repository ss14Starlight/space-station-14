using System.Numerics;
using Content.Shared._FarHorizons.VisualPickupable;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._FarHorizons.VisualPickupable;

public sealed class VisualPickupableSystem : SharedVisualPickupableSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private HashSet<Entity<PickupableVisualsComponent>> _trackedEntities = new();

    private const int frontDrawDepth = 7;
    private const int backDrawDepth = 5;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickupableVisualsComponent, AfterAutoHandleStateEvent>((ent, ref _) => UpdateState(ent));
        SubscribeLocalEvent<PickupableVisualsComponent, MapInitEvent>((ent, ref _) => UpdateState(ent));
    }

    private void UpdateState(Entity<PickupableVisualsComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out _))
            return;

        if (ent.Comp.Source == null)
            _trackedEntities.Remove(ent);
        else if (TryComp<SpriteComponent>(ent.Comp.Source, out _))
            _trackedEntities.Add(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        HashSet<Entity<PickupableVisualsComponent>> toDelete = new();

        var spriteQuery = GetEntityQuery<SpriteComponent>();
        var metaQuery = GetEntityQuery<MetaDataComponent>();
        var transformQuery = GetEntityQuery<TransformComponent>();
        var visualPickupableQuery = GetEntityQuery<VisualPickupableComponent>();
        foreach (var ent in _trackedEntities)
        {
            if (!spriteQuery.TryGetComponent(ent, out var targetSprite) ||
                !spriteQuery.TryGetComponent(ent.Comp.Source, out var sourceSprite) ||
                !metaQuery.TryGetComponent(ent, out var targetMeta) ||
                (targetMeta.Flags & MetaDataFlags.Detached) != 0 ||
                !transformQuery.TryGetComponent(ent.Comp.Source, out var sourceTransform) ||
                !transformQuery.TryGetComponent(sourceTransform.ParentUid, out var parentTransform) ||
                !visualPickupableQuery.TryGetComponent(ent.Comp.Source, out var visualPickupable))
            {
                toDelete.Add(ent);
                continue;
            }

            _sprite.CopySprite((ent.Comp.Source.Value, sourceSprite), (ent, targetSprite));
            _sprite.SetRotation((ent, targetSprite), Angle.FromDegrees(visualPickupable.AngleDegrees));

            var facingDirection = _transform.GetWorldRotation(parentTransform) + _eyeManager.CurrentEye.Rotation;

            if (facingDirection.GetCardinalDir() == Direction.North)
            {
                _sprite.SetOffset((ent, targetSprite), visualPickupable.OffsetBack);
                _sprite.SetDrawDepth((ent, targetSprite), backDrawDepth);
            }
            else
            {
                _sprite.SetOffset((ent, targetSprite), visualPickupable.OffsetFront);
                _sprite.SetDrawDepth((ent, targetSprite), frontDrawDepth);
            }
        }

        _trackedEntities.ExceptWith(toDelete);
    }
}
