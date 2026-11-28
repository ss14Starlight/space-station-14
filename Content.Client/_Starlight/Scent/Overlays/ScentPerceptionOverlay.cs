using Content.Shared._Starlight.Eye;
using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Eye.Blinding.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Starlight.Scent.Overlays;

// Redraws visible ScentMarker sprites after BlindOverlay, DarkenedVisionOverlay, and
// BlurryVisionOverlay paint over the screen. Only draws markers the client already has and
// ScentTrackingSystem still marks Visible.
public sealed partial class ScentPerceptionOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly RemoteControlInterface _remoteControl;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly ContainerSystem _container;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    // Draw at 1 so that the scent overlay shows over BlindOverlay, DarkenedVisionOverlay, and BlurryVisionOverlay.
    public ScentPerceptionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _remoteControl = _entityManager.System<RemoteControlInterface>();
        _sprite = _entityManager.System<SpriteSystem>();
        _transform = _entityManager.System<TransformSystem>();
        _container = _entityManager.System<ContainerSystem>();
        ZIndex = 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_remoteControl.TryGetViewportEntity(args, out var viewer))
            return false;

        return IsVisionObscured(viewer);
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
