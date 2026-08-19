using Content.Shared._Starlight.Eye;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Eye.Blinding.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._Starlight.Scent.Overlays;

// Redraws visible ScentMarker sprites after BlindOverlay, DarkenedVisionOverlay, and
// BlurryVisionOverlay paint over the screen. Only draws markers the client already has and
// ScentTrackingSystem still marks Visible.
public sealed class ScentPerceptionOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly ContainerSystem _container;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    // BlindOverlay, DarkenedVisionOverlay, and BlurryVisionOverlay never set ZIndex, so they
    // draw at the default 0. Anything higher draws after them.
    public ScentPerceptionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entityManager.System<SpriteSystem>();
        _transform = _entityManager.System<TransformSystem>();
        _container = _entityManager.System<ContainerSystem>();
        ZIndex = 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalSession?.AttachedEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        if (_playerManager.LocalSession?.AttachedEntity is not { } player)
            return false;

        return IsVisionObscured(player);
    }

    private bool IsVisionObscured(EntityUid player)
    {
        if (_entityManager.TryGetComponent(player, out BlindableComponent? blindable) && blindable.IsBlind)
            return true;

        if (_entityManager.TryGetComponent(player, out DarkenedVisionComponent? darkened) &&
            darkened.Strength > 0f && darkened.Strength < darkened.BlindTreshold)
            return true;

        if (_entityManager.TryGetComponent(player, out BlurryVisionComponent? blurry) && blurry.Magnitude > 0f)
            return true;

        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        var query = _entityManager.EntityQueryEnumerator<ScentMarkerComponent, SpriteComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var xform, out var meta))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!sprite.Visible)
                continue;

            if (_container.IsEntityInContainer(uid, meta))
                continue;

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);

            if (!args.WorldBounds.Contains(position))
                continue;

            _sprite.RenderSprite((uid, sprite), worldHandle, eyeRotation, rotation, position);
        }
    }
}
