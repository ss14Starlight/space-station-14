using Content.Shared.CCVar;
using Content.Shared._Starlight.Sprites;
using Robust.Client.GameObjects;
using static Robust.Client.GameObjects.SpriteComponent;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Sprites
{
    public sealed class AnimationSyncSystem : EntitySystem
    {
        [Dependency] private readonly SpriteSystem _sprite = default!;
        [Dependency] private readonly IConfigurationManager _config = default!; // Starlight

        private bool _reducedMotion = false;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AnimationSyncComponent, AppearanceChangeEvent>(OnAppearanceChanged);
            _config.OnValueChanged(CCVars.ReducedMotion, (b) => { _reducedMotion = b; }, invokeImmediately: true);
        }

        public void OnAppearanceChanged(EntityUid uid, AnimationSyncComponent component, AppearanceChangeEvent args)
        {
            if (args.Sprite == null)
                return;
            
            UpdateLayerSync(uid, component, args.Sprite);
        }

        public void UpdateLayerSync(EntityUid uid,
            AnimationSyncComponent? component = null,
            SpriteComponent? sprite = null)
        {
            if (!Resolve(uid, ref component, ref sprite))
                return;

            SetAllAutoAnimated((uid, sprite), !(_reducedMotion && component.ReduceMotion));
            SyncToLayer((uid, sprite), component.LayerKey);
        }
        
        /// <summary>
        /// Set AutoAnimated value for all layers of a given entity's sprite
        /// </summary>
        public void SetAllAutoAnimated(Entity<SpriteComponent?> sprite, bool value)
        {
            if (!Resolve(sprite.Owner, ref sprite.Comp))
                return;

            foreach (var spriteLayer in sprite.Comp.AllLayers)
            {
                if (spriteLayer is Layer layer)
                {
                    _sprite.LayerSetAutoAnimated(layer, value);
                }
            }
        }

        #region Layer Synchro
        /// <summary>
        /// Synchronizes the layers of a SpriteComponent to given layer's current animation time
        /// </summary>
        /// <param name="sprite">Sprite to synchronize</param>
        /// <param name="key">Key of the layer to synchronize with</param>
        public void SyncToLayer(Entity<SpriteComponent?> sprite, string key)
        {
            if (!Resolve(sprite.Owner, ref sprite.Comp)
                || !_sprite.TryGetLayer(sprite, key, out var layer, true))
                return;

            var animTime = LayerGetAnimationTime(layer);
            _sprite.SetAutoAnimateSync(sprite.Comp, animTime);
        }
        #endregion

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
