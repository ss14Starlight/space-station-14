using Robust.Client.GameObjects;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Starlight.Sprites
{
    public sealed class AnimationSyncSystem : EntitySystem
    {
        [Dependency] private readonly SpriteSystem _sprite = default!;

        /// <summary>
        /// Synchronizes the layers of a SpriteComponent to given layer's current animation time
        /// </summary>
        /// <param name="sprite">Sprite to synchronize</param>
        /// <param name="key">Key of the layer to synchronize with</param>
        public void SyncToLayer(Entity<SpriteComponent?> sprite, Enum key)
        {
            if (!Resolve(sprite.Owner, ref sprite.Comp)
                || !_sprite.TryGetLayer(sprite, key, out var layer, true))
                return;

            var animTime = LayerGetAnimationTime(layer);
            _sprite.SetAutoAnimateSync(sprite.Comp, animTime);
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
