using System.Linq;
using Robust.Client.GameObjects;
using static Robust.Client.GameObjects.SpriteComponent;

// ReSharper disable CheckNamespace
namespace Content.Client.Light;

public sealed partial class RgbLightControllerSystem
{
    private readonly Dictionary<EntityUid, Dictionary<Layer, Color>> _autoOriginalColors = new();

    private static bool IsLayerOf(SpriteComponent sprite, Layer layer) =>
        sprite.LayerData.Any(other => ReferenceEquals(other, layer));
}
