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
    /// Sheet used for paw prints. Separate from <see cref="Sprites"/> because state is tracked
    /// per print, so one tile can hold both kinds.
    /// </summary>
    [DataField]
    public ResPath PawSprites = new("/Textures/_Starlight/Effects/pawprints.rsi");
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
}
