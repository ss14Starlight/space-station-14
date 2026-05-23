using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Shared.SubFloor;
using Content.Shared.VentCrawl;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._Starlight.VentCrawl;

public sealed partial class VentCrawPipeOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly SpriteSystem _spriteSystem;
    private readonly EntityLookupSystem _lookup;

    private static readonly Color PipeGlowColor = new Color(0.5f, 0.85f, 1.0f, 0.4f);
    private const float GlowRadius = 0.015f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public VentCrawPipeOverlay()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entityManager.System<SpriteSystem>();
        _lookup = _entityManager.System<EntityLookupSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null) return false;

        return _entityManager.TryGetComponent<VentCrawlerComponent>(player, out var comp)
               && comp.InTube;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null) return;

        var worldHandle = args.WorldHandle;
        var bounds = args.WorldBounds;

        var pipeQuery = _entityManager.GetEntityQuery<SpriteComponent>();
        var subFloorQuery = _entityManager.GetEntityQuery<SubFloorHideComponent>();
        var pipeAppQuery = _entityManager.GetEntityQuery<PipeAppearanceComponent>();

        var entities = _lookup.GetEntitiesIntersecting(
            args.MapId,
            bounds,
            LookupFlags.Uncontained
        );

        worldHandle.UseShader(null);

        foreach (var uid in entities)
        {
            if (!pipeAppQuery.HasComponent(uid)) continue;
            if (!pipeQuery.TryGetComponent(uid, out var sprite)) continue;
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

            var oldColor = sprite.Color;
            _spriteSystem.SetColor((uid, sprite), PipeGlowColor);

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

            _spriteSystem.SetColor((uid, sprite), new Color(0.8f, 0.95f, 1.0f, 1.0f));

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
