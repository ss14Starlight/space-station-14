using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Shared._Starlight.VentCrawl.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Content.Shared._Starlight.VentCrawl.EntitySystems;

namespace Content.Client._Starlight.VentCrawl;

public sealed partial class VentCrawPipeOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly SpriteSystem _spriteSystem;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _xformSys = default!;

    private static readonly Color _pipeGlowColor = new(0.4f, 0.8f, 1.0f, 0.35f);
    private static readonly Color _pipeBaseColor = new(0.75f, 0.92f, 1.0f, 1.0f);
    private static readonly Vector2[] _glowOffsets =
    {
        new(-GlowRadius, 0), new(GlowRadius, 0),
        new(0, -GlowRadius), new(0,  GlowRadius),
    };
    private const float GlowRadius = 0.015f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public VentCrawPipeOverlay()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entityManager.System<SpriteSystem>();
        _lookup = _entityManager.System<EntityLookupSystem>();
        _xformSys = _entityManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null) return;

        if (!_entityManager.TryGetComponent<BeingVentCrawlComponent>(player, out var beingCrawl))
            return;

        if (!_entityManager.TryGetComponent<VentCrawlHolderComponent>(beingCrawl.Holder, out var holder))
            return;

        var playerLayer = ResolvePlayerLayer(holder);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(null);

        var eyeRot = _entityManager.GetComponent<EyeComponent>(player.Value).Rotation;

        var query = _entityManager.EntityQueryEnumerator<
            PipeAppearanceComponent,
            SpriteComponent,
            TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var sprite, out _))
        {
            if (!sprite.Visible)
                continue;

            if (!_entityManager.HasComponent<VentCrawlManifoldComponent>(uid))
            {
                if (!_entityManager.TryGetComponent<AtmosPipeLayersComponent>(uid, out var pipeLayer))
                    continue;

                if (pipeLayer.CurrentPipeLayer != playerLayer)
                    continue;
            }

            var worldPos = _xformSys.GetWorldPosition(uid);

            if (!args.WorldBounds.Contains(worldPos))
                continue;

            var worldRot = _xformSys.GetWorldRotation(uid);

            var oldColor = sprite.Color;

            _spriteSystem.SetColor((uid, sprite), _pipeGlowColor);

            foreach (var offset in _glowOffsets)
            {
                _spriteSystem.RenderSprite(
                    (uid, sprite),
                    args.WorldHandle,
                    eyeRot,
                    worldRot,
                    worldPos + offset);
            }

            _spriteSystem.SetColor((uid, sprite), _pipeBaseColor);

            _spriteSystem.RenderSprite(
                (uid, sprite),
                args.WorldHandle,
                eyeRot,
                worldRot,
                worldPos);

            _spriteSystem.SetColor((uid, sprite), oldColor);
        }
    }

    private AtmosPipeLayer ResolvePlayerLayer(VentCrawlHolderComponent holder)
    {
        if (holder.CurrentTube != null
            && _entityManager.HasComponent<VentCrawlManifoldComponent>(holder.CurrentTube.Value)
            && holder.ManifoldLayer != null)
            return holder.ManifoldLayer.Value;

        if (holder.CurrentTube != null
            && _entityManager.TryGetComponent<AtmosPipeLayersComponent>(holder.CurrentTube.Value, out var cur))
            return cur.CurrentPipeLayer;

        if (holder.NextTube != null
            && _entityManager.TryGetComponent<AtmosPipeLayersComponent>(holder.NextTube.Value, out var next))
            return next.CurrentPipeLayer;

        return AtmosPipeLayer.Primary;
    }
}
