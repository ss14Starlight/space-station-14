// ReSharper disable CheckNamespace
using Content.Shared.DisplacementMap;
using Robust.Client.GameObjects;

namespace Content.Client.Clothing;

public sealed partial class ClientClothingSystem
{
    private void PreserveUnshadedDisplacement(
        Entity<SpriteComponent> wearer,
        object layerKey,
        PrototypeLayerData layer,
        DisplacementData displacement)
    {
        if (layer.Shader != "unshaded")
            return;

        var shader = displacement.ShaderOverride switch
        {
            "DisplacedDraw" => "DisplacedDrawUnshaded",
            "BorgHeadwearDisplacedDraw" => "BorgHeadwearDisplacedDrawUnshaded",
            _ => null,
        };

        if (shader != null)
            wearer.Comp.LayerSetShader(layerKey, shader);
    }
}
