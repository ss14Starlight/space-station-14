using Robust.Client.GameObjects;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Starlight.Sprites
{
    public sealed class AnimationSyncSystem : EntitySystem
    {
        [Dependency] private SpriteSystem _sprite = default!;

        public void SyncOnLayer(Entity<SpriteComponent?> sprite, Enum key)
        {
            if (!Resolve(sprite.Owner, ref sprite.Comp)
                || !_sprite.TryGetLayer(sprite, key, out var layer, true))
                return;

            var animTime = LayerGetAnimationTime(layer);
            _sprite.SetAutoAnimateSync(sprite.Comp, animTime);
        }

        public void SyncOnLayer(SpriteComponent sprite, Layer layer)
        {
            var animTime = LayerGetAnimationTime(layer);
            _sprite.SetAutoAnimateSync(sprite, animTime);
        }

        // RT doesn't include getters for layer anim data so including here for future use.
        #region AnimationTime Getters
        public float LayerGetAnimationTime(Entity<SpriteComponent?> sprite, int index)
        {
            return _sprite.TryGetLayer(sprite, index, out var layer, true) ? LayerGetAnimationTime(layer) : 0f;
        }

        public float LayerGetAnimationTime(Entity<SpriteComponent?> sprite, Enum key)
        {
            return _sprite.TryGetLayer(sprite, key, out var layer, true) ? LayerGetAnimationTime(layer) : 0f;
        }

        public float LayerGetAnimationTime(Entity<SpriteComponent?> sprite, string key)
        {
            return _sprite.TryGetLayer(sprite, key, out var layer, true) ? LayerGetAnimationTime(layer) : 0f;
        }

        public float LayerGetAnimationTime(Layer layer)
        {
            return layer.AnimationTime;
        }
        #endregion
    }
}