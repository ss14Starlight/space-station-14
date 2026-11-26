// ReSharper disable CheckNamespace
namespace Content.Shared.Maps;

public sealed partial class ContentTileDefinition
{
    /// <summary>
    /// Space shows through this tile, as with glass floors. Bounce lighting treats it as open space, so it gives off
    /// starlight instead of being read as a lit floor.
    /// </summary>
    /// <remarks>
    /// Was added to use instead of isSpace, since isSpace also applied to lattice, which breaks space light on them(i.e. they dark, while plating which placed near will have light from stars)
    /// </remarks>
    [DataField]
    public bool ShowsSpace { get; private set; }

    /// <summary>
    /// How glossy the tile is, from 0 for a matte floor to 1 for a mirror polish. Glossy floors catch the light of
    /// nearby lamps.
    /// </summary>
    [DataField]
    public float Shine { get; private set; }
}
