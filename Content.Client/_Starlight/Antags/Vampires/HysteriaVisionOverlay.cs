using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
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
    private readonly EntityLookupSystem _lookup;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    // Cache of which sprite index to show for each humanoid (randomized per-entity)
    private readonly Dictionary<EntityUid, int> _entitySpriteIndex = new();

    // Cached RSI states for each disguise type
    private readonly RSI.State?[] _disguiseStates = new RSI.State?[HysteriaVisionComponent.DisguiseSprites.Length];
    private bool _spritesLoaded;

    public HysteriaVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<TransformSystem>();
        _lookup = _entManager.System<EntityLookupSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalEntity;
        if (player == null 
            || !_entManager.TryGetComponent<HysteriaVisionComponent>(player, out var hysteria) 
            || _timing.CurTime > hysteria.EndTime) // Check if effect expired
            return false;

        // Load all sprites if not loaded
        if (!_spritesLoaded)
            LoadDisguiseSprites();

        return _spritesLoaded;
    }

    private void LoadDisguiseSprites()
    {
        _spritesLoaded = true;

        for (var i = 0; i < HysteriaVisionComponent.DisguiseSprites.Length; i++)
        {
            var sprite = HysteriaVisionComponent.DisguiseSprites[i];
            var trimmedPath = sprite.Path.TrimStart('/');
            var path = new ResPath("/Textures") / trimmedPath;

            if (!_resourceCache.TryGetResource<RSIResource>(path, out var rsiResource) 
                || !rsiResource.RSI.TryGetState(sprite.State, out var rsiState))
            {
                _disguiseStates[i] = null;
                continue;
            }

            _disguiseStates[i] = rsiState;
        }
    }

    /// <summary>
    /// Gets the sprite index for a given entity, assigning a random one if not yet assigned.
    /// </summary>
    private int GetSpriteIndexForEntity(EntityUid uid)
    {
        if (_entitySpriteIndex.TryGetValue(uid, out var index))
            return index;

        index = _random.Next(HysteriaVisionComponent.DisguiseSprites.Length);
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
        if (player == null || !_entManager.TryGetComponent<HysteriaVisionComponent>(player, out var hysteria))
            return;

        var preserveSourceThrallVisibility =
            _entManager.TryGetComponent<VampireThrallComponent>(player.Value, out var playerThrall)
            && playerThrall.Master == hysteria.Source;

        var worldHandle = args.WorldHandle;
        var counterRotation = -(args.Viewport.Eye?.Rotation ?? Angle.Zero); 

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
                && _entManager.TryGetComponent<VampireThrallComponent>(uid, out var thrall)
                && thrall.Master == hysteria.Source)
                continue;

            // Get world position
            var worldPos = _transform.GetWorldPosition(xform);

            // Check if in viewport bounds (with some margin)
            if (!args.WorldBounds.Enlarged(2f).Contains(worldPos))
                continue;

            // Get random sprite for this entity
            var spriteIndex = GetSpriteIndexForEntity(uid);
            var disguiseState = _disguiseStates[spriteIndex];
            if (disguiseState == null)
                continue;

            var size = HysteriaVisionComponent.DisguiseSprites[spriteIndex].Size;

            // Get the direction from the targets sprite to match their facing
            var rsiDir = GetRsiDirection(xform.LocalRotation.GetCardinalDir());
            var texture = disguiseState.GetFrame(rsiDir, 0);
            if (texture == null)
                continue;

            // Calculate the draw box centered on the entity
            var drawPos = worldPos;
            
            var box = Box2.CenteredAround(drawPos, size);

            var rotatedBox = new Box2Rotated(box, counterRotation, drawPos);
            worldHandle.DrawTextureRect(texture, rotatedBox);
        }
    }
}
