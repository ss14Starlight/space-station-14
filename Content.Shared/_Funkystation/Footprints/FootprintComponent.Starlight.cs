using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.Footprints;

public sealed partial class FootprintComponent : Component
{
    /// <summary>
    /// The solution stored directly on every footprint entity.
    /// </summary>
    public const string SolutionName = "print";

    [DataField]
    public ResPath Sprites = new("/Textures/_Funkystation/Effects/footprints.rsi");

    /// <summary>
    /// Species print shapes. Separate from <see cref="Sprites"/> because state is per print,
    /// so one tile can hold several kinds, and because the sheets carry different licenses.
    /// </summary>
    [DataField]
    public ResPath SpeciesSprites = new("/Textures/_Starlight/Effects/speciesprints.rsi");
    /// <summary>
    /// Number of print layers already configured by the client.
    /// </summary>
    [ViewVariables]
    public int RenderedPrintCount;

    /// <summary>
    /// The shared RGB tint for every print on this tile. Individual print opacity is stored separately.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Color BaseColor = Color.White;
}

[Serializable, NetSerializable]
public enum FootprintVisualState : byte
{
    Foot,
    Dragging1,
    Dragging2,
    Dragging3,
    Dragging4,
    Dragging5,
    Paw, // added at end; would cause desync issues if put before dragging
    Barefoot,
    Claw,
    Talon,
    Moth,
    Spider,
    Diona,
}
