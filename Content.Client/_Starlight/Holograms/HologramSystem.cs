using System.Numerics;
using Content.Shared._Starlight.Holograms;
using Content.Shared._Starlight.Holograms.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Holograms;

public sealed class HologramSystem : SharedHologramSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EyeSystem _eyeSystem = default!;
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HologramProjectedComponent, ComponentShutdown>(OnProjectedShutdown);
    }

    private void OnProjectedShutdown(EntityUid uid, HologramProjectedComponent component, ComponentShutdown args)
    {
        // Delete client-side effect
        DeleteEffect(component);
        
        // Clear eye target on client
        if (component.SetEyeTarget && TryComp<EyeComponent>(uid, out var eyeComp))
            _eyeSystem.SetTarget(uid, null, eyeComp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        var player = _player.LocalSession?.AttachedEntity;
        if (TryComp<HologramProjectedComponent>(player, out var holoProjComp))
        {
            ProjectedUpdate(player.Value, holoProjComp); // This makes it so only the currently controlled entity is predicted, assuming they're a hologram.

            // Check if we should be setting the eye target of the hologram.
            if (holoProjComp.SetEyeTarget && TryComp<EyeComponent>(player.Value, out var eyeComp) && 
                holoProjComp.CurProjector != null && TryGetEntity(holoProjComp.CurProjector.Value, out var projectorEntity))
                _eyeSystem.SetTarget(player.Value, projectorEntity, eyeComp);
        }
        
        // Handle projected effects for all holograms
        HandleProjectedEffects();
    }
    
    private void HandleProjectedEffects()
    {
        var query = EntityManager.EntityQueryEnumerator<HologramProjectedComponent>();
        while (query.MoveNext(out var hologram, out var component))
        {
            if (component.CurProjector == null || !TryGetEntity(component.CurProjector.Value, out var projectorEntity))
            {
                DeleteEffect(component);
                continue;
            }

            if (component.EffectPrototype == null)
            {
                DeleteEffect(component);
                continue;
            }

            var holoXform = Transform(hologram);
            var holoCoords = _transform.GetMoverCoordinates(hologram, holoXform);

            var projXform = Transform(projectorEntity.Value);
            var projCoords = _transform.GetMoverCoordinates(projectorEntity.Value, projXform);

            if (holoCoords.EntityId != projCoords.EntityId)
            {
                DeleteEffect(component);
                continue;
            }

            var originPos = projCoords.Position;

            // Add the effect's offset, if applicable
            if (TryComp<HologramProjectorComponent>(projectorEntity.Value, out var projComp))
            {
                var direction = projXform.LocalRotation.GetCardinalDir();

                var offset = direction switch
                {
                    Direction.North => projComp.EffectOffsets[Direction.South],
                    Direction.South => projComp.EffectOffsets[Direction.North],
                    Direction.East => projComp.EffectOffsets[Direction.West],
                    Direction.West => projComp.EffectOffsets[Direction.East],
                    _ => Vector2.Zero
                };

                originPos += offset;
            }

            // Determine middle point between hologram and projector
            var effectPos = (holoCoords.Position + originPos) / 2;

            // Determine rotation that points from projector to hologram
            var effectRot = (holoCoords.Position - originPos).ToAngle() - MathHelper.PiOver2;
            
            // Calculate distance for scaling
            var distance = (holoCoords.Position - originPos).Length();

            var effectCoords = new EntityCoordinates(holoCoords.EntityId, effectPos);
            if (!effectCoords.IsValid(EntityManager))
            {
                DeleteEffect(component);
                continue;
            }

            // Spawn or update the effect entity
            if (component.EffectEntity == null || !Exists(component.EffectEntity.Value))
            {
                component.EffectEntity = Spawn(component.EffectPrototype, effectCoords);
            }
            else
            {
                _transform.SetCoordinates(component.EffectEntity.Value, effectCoords);
            }

            _transform.SetLocalRotation(component.EffectEntity.Value, effectRot);
            
            // Scale the sprite to match the distance
            _spriteSystem.SetScale(component.EffectEntity.Value, new Vector2(1f, distance));
        }
    }
    
    private void DeleteEffect(HologramProjectedComponent component)
    {
        if (component.EffectEntity != null && Exists(component.EffectEntity.Value))
            QueueDel(component.EffectEntity.Value);

        component.EffectEntity = null;
    }
}
