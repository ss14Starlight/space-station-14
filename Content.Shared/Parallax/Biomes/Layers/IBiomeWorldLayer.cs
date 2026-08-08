using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared.Parallax.Biomes.Layers;

/// <summary>
/// Handles actual objects such as decals and entities.
/// </summary>
public partial interface IBiomeWorldLayer : IBiomeLayer
{
    /// <summary>
    /// What tiles we're allowed to spawn on, real or biome.
    /// </summary>
    List<ProtoId<ContentTileDefinition>> AllowedTiles { get; }

    //Starlight - Begin
    /// <summary>
    /// When true, allows spawning on any floor tile regardless of AllowedTiles.
    /// </summary>
    bool AllowAllTiles { get; }
    //Starlight - End
}
