using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Shared.VentCrawl.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._Starlight.VentCrawl;

public sealed partial class VentCrawPipeOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly SpriteSystem _spriteSystem;
    private readonly EntityLookupSystem _lookup;

    private static readonly Color PipeGlowColor = new(0.5f, 0.85f, 1.0f, 0.4f);
    private static readonly Color CurrentPipeGlowColor = new(1.0f, 0.45f, 0.45f, 0.4f);
    private const float GlowRadius = 0.015f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public VentCrawPipeOverlay()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entityManager.System<SpriteSystem>();
        _lookup = _entityManager.System<EntityLookupSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null) return;

        if (!_entityManager.TryGetComponent<BeingVentCrawlComponent>(player, out var component))
            return;

        if (!_entityManager.TryGetComponent<VentCrawlHolderComponent>(component.Holder, out var holder))
            return;

        var worldHandle = args.WorldHandle;
        var bounds = args.WorldBounds;

        var entities = _lookup.GetEntitiesIntersecting(
            args.MapId,
            bounds,
            LookupFlags.Uncontained
        );

        worldHandle.UseShader(null);

        foreach (var uid in entities)
        {
            if (!_entityManager.HasComponent<PipeAppearanceComponent>(uid)) continue;
            if (!_entityManager.TryGetComponent<SpriteComponent>(uid, out var sprite)) continue;
            if (!sprite.Visible) continue;

            var xform = _entityManager.GetComponent<TransformComponent>(uid);
            var worldPos = xform.WorldPosition;
            var worldRot = xform.WorldRotation;

            var offsets = new Vector2[]
            {
                new(-GlowRadius, 0), new(GlowRadius, 0),
                new(0, -GlowRadius), new(0, GlowRadius),
            };

            var eyeRot = _entityManager.GetComponent<EyeComponent>(player.Value).Rotation;

            var isCurrentTube = holder.CurrentTube == uid;
            var glowColor = isCurrentTube ? CurrentPipeGlowColor : PipeGlowColor;
            var baseColor = isCurrentTube ? new Color(1.0f, 0.6f, 0.6f, 1.0f) : new Color(0.8f, 0.95f, 1.0f, 1.0f);

            var oldColor = sprite.Color;

            _spriteSystem.SetColor((uid, sprite), glowColor);

            foreach (var offset in offsets)
            {
                _spriteSystem.RenderSprite(
                    (uid, sprite),
                    worldHandle,
                    eyeRot,
                    worldRot,
                    worldPos + offset
                );
            }

            _spriteSystem.SetColor((uid, sprite), baseColor);

            _spriteSystem.RenderSprite(
                (uid, sprite),
                worldHandle,
                eyeRot,
                worldRot,
                worldPos
            );

            _spriteSystem.SetColor((uid, sprite), oldColor);
        }
    }
}
