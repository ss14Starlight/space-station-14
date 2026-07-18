using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Content.Shared.Body.Components;

namespace Content.Client._Starlight.Overlay.Overlays;

public abstract partial class BaseEntityHighlightOverlay : BaseVisionOverlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    private readonly ContainerSystem _containerSystem;
    private readonly TransformSystem _transform = default!;
    private readonly SpriteSystem _sprite = default!;
    public BaseEntityHighlightOverlay(ShaderPrototype shader) : base(shader)
    {
        _containerSystem = _entityManager.System<ContainerSystem>();
        _transform = _entityManager.System<TransformSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var worldHandle = args.WorldHandle;
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        worldHandle.UseShader(_shader);
        var query = _entityManager.EntityQueryEnumerator<BodyComponent, MetaDataComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var meta, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId || _containerSystem.IsEntityInContainer(uid, meta)) continue;
            var (position, rotation) = _transform.GetWorldPositionRotation(xform);

            _sprite.RenderSprite((uid, sprite), worldHandle, eyeRotation, rotation, position, null);
        }

        worldHandle.UseShader(null);
    }
}
