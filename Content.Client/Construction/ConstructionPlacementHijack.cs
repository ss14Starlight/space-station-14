using System.Linq;
using Content.Shared.Construction.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Placement;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Construction
{
    public sealed class ConstructionPlacementHijack : PlacementHijack
    {
        private readonly ConstructionSystem _constructionSystem;
        private readonly ConstructionPrototype? _prototype;

        public ConstructionSystem? CurrentConstructionSystem { get { return _constructionSystem; } }
        public ConstructionPrototype? CurrentPrototype { get { return _prototype; } }

        public override bool CanRotate { get; }

        public ConstructionPlacementHijack(ConstructionSystem constructionSystem, ConstructionPrototype? prototype)
        {
            _constructionSystem = constructionSystem;
            _prototype = prototype;
            CanRotate = prototype?.CanRotate ?? true;
        }

        /// <inheritdoc />
        public override bool HijackPlacementRequest(EntityCoordinates coordinates)
        {
            if (_prototype != null)
            {
                var dir = Manager.Direction;
                _constructionSystem.SpawnGhost(_prototype, coordinates, dir);
            }
            return true;
        }

        /// <inheritdoc />
        public override bool HijackDeletion(EntityUid entity)
        {
            if (IoCManager.Resolve<IEntityManager>().HasComponent<ConstructionGhostComponent>(entity))
            {
                _constructionSystem.ClearGhost(entity.GetHashCode());
            }
            return true;
        }

        /// <inheritdoc />
        public override void StartHijack(PlacementManager manager)
        {
            base.StartHijack(manager);

            if (_prototype is null || !_constructionSystem.TryGetRecipePrototype(_prototype.ID, out var targetProtoId))
                return;

            if (!IoCManager.Resolve<IPrototypeManager>().TryIndex(targetProtoId, out EntityPrototype? proto))
                return;

            manager.CurrentTextures = SpriteComponent.GetPrototypeTextures(proto, IoCManager.Resolve<IResourceCache>()).ToList();

            // Starlight START

            // Make the whole thing transparent just like a placed ghost. It's impossible to see e.g. pipes if they're
            // obscured by the main sprite body.
            if (manager.CurrentMode is { } mode)
            {
                mode.ValidPlaceColor = mode.ValidPlaceColor.WithAlpha(0.5f);
                mode.InvalidPlaceColor = mode.InvalidPlaceColor.WithAlpha(0.5f);
            }

            // Fix per-layer/sprite Offset being lost by manually applying it to the ghost layers directly.
            var entMan = IoCManager.Resolve<IEntityManager>();
            if (proto.TryGetComponent<SpriteComponent>(out var sprite, entMan.ComponentFactory)
                && manager.CurrentPlacementOverlayEntity is { } overlay
                && entMan.TryGetComponent<SpriteComponent>(overlay, out var overlaySprite))
            {
                var sprites = entMan.System<SpriteSystem>();
                var i = 0;

                foreach (var layer in sprite.AllLayers)
                {
                    if (layer.ActualRsi?.Path == null || layer.RsiState.Name == null)
                        continue;

                    sprites.LayerSetOffset((overlay, overlaySprite), i, sprite.Offset + ((SpriteComponent.Layer)layer).Offset);
                    i++;
                }

                // Fix NoRotation also being ignored.
                overlaySprite.NoRotation = sprite.NoRotation;
            }
            // Starlight END
        }
    }
}
