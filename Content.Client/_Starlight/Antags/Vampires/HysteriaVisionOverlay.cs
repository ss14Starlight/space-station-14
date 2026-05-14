using System.Numerics;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Antags.Vampires;

/// <summary>
/// Overlay that renders monster/animal sprites over humanoids
/// when the local player has HysteriaVisionComponent.
/// </summary>
public sealed class HysteriaVisionOverlay : Robust.Client.Graphics.Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly TransformSystem _transform;
    private readonly EntityQuery<HysteriaVisionComponent> _hysteriaQuery;
    private readonly EntityQuery<VampireThrallComponent> _thrallQuery;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    // Cache of which sprite index to show for each humanoid (randomized per-entity)
    private readonly Dictionary<EntityUid, int> _entitySpriteIndex = new();

    // Cached RSI states for each disguise type
    private readonly List<RSI.State?> _disguiseStates = new();
    private readonly List<HysteriaDisguiseSprite> _loadedDisguiseSprites = new();
    private bool _spritesLoaded;

    public HysteriaVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<TransformSystem>();
        _hysteriaQuery = _entManager.GetEntityQuery<HysteriaVisionComponent>();
        _thrallQuery = _entManager.GetEntityQuery<VampireThrallComponent>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalEntity;
        if (player == null
            || !_hysteriaQuery.TryGetComponent(player.Value, out var hysteria)
            || _timing.CurTime > hysteria.EndTime) // Check if effect expired
            return false;

        if (hysteria.DisguiseSprites.Count == 0)
            return false;

        EnsureDisguiseSpritesLoaded(hysteria);

        return _spritesLoaded;
    }

    private void EnsureDisguiseSpritesLoaded(HysteriaVisionComponent hysteria)
    {
        if (_spritesLoaded && IsSpriteCacheCurrent(hysteria))
            return;

        LoadDisguiseSprites(hysteria);
    }

    private bool IsSpriteCacheCurrent(HysteriaVisionComponent hysteria)
    {
        if (_loadedDisguiseSprites.Count != hysteria.DisguiseSprites.Count)
            return false;

        for (var i = 0; i < hysteria.DisguiseSprites.Count; i++)
        {
            if (!_loadedDisguiseSprites[i].Equals(hysteria.DisguiseSprites[i]))
                return false;
        }

        return true;
    }

    private void LoadDisguiseSprites(HysteriaVisionComponent hysteria)
    {
        _spritesLoaded = true;
        _disguiseStates.Clear();
        _loadedDisguiseSprites.Clear();
        _entitySpriteIndex.Clear();

        for (var i = 0; i < hysteria.DisguiseSprites.Count; i++)
        {
            var sprite = hysteria.DisguiseSprites[i];
            _loadedDisguiseSprites.Add(sprite);
            var trimmedPath = sprite.Path.TrimStart('/');
            var path = new ResPath("/Textures") / trimmedPath;

            if (!_resourceCache.TryGetResource<RSIResource>(path, out var rsiResource)
                || !rsiResource.RSI.TryGetState(sprite.State, out var rsiState))
            {
                _disguiseStates.Add(null);
                continue;
            }

            _disguiseStates.Add(rsiState);
        }
    }

    /// <summary>
    /// Gets the sprite index for a given entity, assigning a random one if not yet assigned.
    /// </summary>
    private int GetSpriteIndexForEntity(EntityUid uid, int spriteCount)
    {
        if (_entitySpriteIndex.TryGetValue(uid, out var index))
            return index;

        index = _random.Next(spriteCount);
        _entitySpriteIndex[uid] = index;
        return index;
    }

    /// <summary>
    /// Converts a Direction into the corresponding RsiDirection
    /// </summary>
    private static RsiDirection GetRsiDirection(Direction dir) => dir switch
    {
        Direction.North => RsiDirection.North,
        Direction.South => RsiDirection.South,
        Direction.East => RsiDirection.East,
        Direction.West => RsiDirection.West,
        Direction.NorthEast => RsiDirection.North,
        Direction.NorthWest => RsiDirection.North,
        Direction.SouthEast => RsiDirection.South,
        Direction.SouthWest => RsiDirection.South,
        _ => RsiDirection.South
    };

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalEntity;
        if (player == null || !_hysteriaQuery.TryGetComponent(player.Value, out var hysteria))
            return;

        var spriteCount = hysteria.DisguiseSprites.Count;
        if (spriteCount == 0)
            return;

        EnsureDisguiseSpritesLoaded(hysteria);

        var preserveSourceThrallVisibility =
            _thrallQuery.TryGetComponent(player.Value, out var playerThrall)
            && playerThrall.Master == hysteria.Source;

        var worldHandle = args.WorldHandle;
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        // Query all humanoids
        var query = _entManager.EntityQueryEnumerator<HumanoidAppearanceComponent, TransformComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out _, out var xform, out var sprite))
        {
            if (xform.MapID != args.MapId // Skip if not on the same map
                || uid == player // Skip self
                || !sprite.Visible) // Skip entities that are not visible
                continue;

            // Skip thralls of the source vampire
            if (preserveSourceThrallVisibility
                && _thrallQuery.TryGetComponent(uid, out var thrall)
                && thrall.Master == hysteria.Source)
                continue;

            var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);

            // Check if in viewport bounds (with some margin)
            if (!args.WorldBounds.Enlarged(2f).Contains(worldPos))
                continue;

            // Get random sprite for this entity
            var spriteIndex = GetSpriteIndexForEntity(uid, spriteCount);
            if (spriteIndex >= _disguiseStates.Count)
                continue;

            var disguiseState = _disguiseStates[spriteIndex];
            if (disguiseState == null)
                continue;

            var size = hysteria.DisguiseSprites[spriteIndex].Size;

            // Get the direction from the targets sprite to match their facing
            var rsiDir = GetRsiDirection(xform.LocalRotation.GetCardinalDir());
            var texture = disguiseState.GetFrame(rsiDir, 0);
            if (texture == null)
                continue;

            var angle = (worldRot + eyeRotation).Reduced().FlipPositive();
            var cardinal = !sprite.NoRotation && sprite.SnapCardinals
                ? angle.RoundToCardinalAngle()
                : Angle.Zero;

            var entityMatrix = Matrix3Helpers.CreateTransform(
                worldPos,
                sprite.NoRotation ? -eyeRotation : worldRot - cardinal);
            var spriteMatrix = Matrix3x2.Multiply(sprite.LocalMatrix, entityMatrix);

            worldHandle.SetTransform(spriteMatrix);
            worldHandle.DrawTextureRect(texture, Box2.FromDimensions(size / -2f, size));
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
    }
}
