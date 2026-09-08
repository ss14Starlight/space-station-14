using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using System.Linq;

namespace Content.Client._Starlight.Humanoid.IgnoreHumanoids;

/// <summary>
/// Stops drones from telling people apart.
/// </summary>
public sealed class IgnoreHumanoidsOverlay : Robust.Client.Graphics.Overlay
{
    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _spriteSystem;
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly Dictionary<EntityUid, (EntityUid Effect, bool WasVisible)> _effectList = [];

    public IgnoreHumanoidsOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        _transform = _entManager.EntitySysManager.GetEntitySystem<SharedTransformSystem>();
        _spriteSystem = _entManager.EntitySysManager.GetEntitySystem<SpriteSystem>();
    }

    /// <summary>
    /// Yeah we technically aren't directly drawing anything here.
    /// If I made it an entity system there would be some overhead, though...
    /// </summary>
    protected override void Draw(in OverlayDrawArgs args)
    {
        var spriteQuery = _entManager.GetEntityQuery<SpriteComponent>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        var query = _entManager.EntityQueryEnumerator<HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!spriteQuery.TryGetComponent(uid, out var sprite))
                continue;

            if (!xformQuery.TryGetComponent(uid, out var xform))
                continue;

            if (sprite.Visible && !_effectList.ContainsKey(uid))
            {
                var wasVisible = sprite.Visible;
                _spriteSystem.SetVisible(uid, false);
                var effect = _entManager.SpawnEntity("EffectUnknownHumanoid", xform.Coordinates);
                _effectList.Add(uid, (effect, wasVisible));
            }
        }

        foreach (var (underlying, effect) in _effectList.ToList())
        {
            if (_entManager.Deleted(underlying))
            {
                if (!_entManager.Deleted(effect.Effect))
                    _entManager.DeleteEntity(effect.Effect);
                _effectList.Remove(underlying);
                continue;
            }

            if (!xformQuery.TryGetComponent(underlying, out var underlyingxform))
                continue;

            if (!xformQuery.TryGetComponent(effect.Effect, out _))
                continue;

            var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(underlyingxform);
            _transform.SetWorldPositionRotation(effect.Effect, worldPosition, worldRotation);
        }
    }

    public void Reset()
    {
        // Copy to list to avoid collection modification during iteration
        var effects = _effectList.ToList();
        _effectList.Clear();

        foreach (var kvp in effects)
        {
            var underlying = kvp.Key;
            var (effect, wasVisible) = kvp.Value;

            // Check if effect entity is not already deleted/terminating before deleting
            if (!_entManager.Deleted(effect))
                _entManager.DeleteEntity(effect);

            // Check if underlying entity is still valid before making it visible again
            if (!_entManager.Deleted(underlying) && wasVisible)
                _spriteSystem.SetVisible(underlying, true);
        }
    }
}
