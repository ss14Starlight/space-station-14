using Content.Client.Options;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.Movement.Systems;

/// <summary>
/// Controls the switching of motion and standing still animation
/// </summary>
public sealed class ClientSpriteMovementSystem : SharedSpriteMovementSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<SpriteMovementComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<SpriteMovementComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_spriteQuery.TryGetComponent(ent, out var sprite))
            return;

        if (ent.Comp.IsMoving)
        {
            foreach (var (layer, state) in ent.Comp.MovementLayers)
            {
                if(!TryComp(ent.Owner, out OptionsVisualizerComponent? _)   // Starlight | Only apply secondary check if an OptionVisualizer is involved,
                || _sprite.LayerExists((ent.Owner, sprite), layer))         // Starlight | so we don't accidentally silence errors we do want to see
                    _sprite.LayerSetData((ent.Owner, sprite), layer, state);
            }
        }
        else
        {
            foreach (var (layer, state) in ent.Comp.NoMovementLayers)
            {
                if(!TryComp(ent.Owner, out OptionsVisualizerComponent? _)   // Starlight | Not doing this throws errors if an entity is
                || _sprite.LayerExists((ent.Owner, sprite), layer))         // Starlight | spawned while no client is there to see it
                    _sprite.LayerSetData((ent.Owner, sprite), layer, state);
            }
        }
    }
}
