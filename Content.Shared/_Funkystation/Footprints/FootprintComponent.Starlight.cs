using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.Footprints;

public sealed partial class FootprintComponent : Component
{
    [DataField]
    public ResPath Sprites = new("/Textures/_Funkystation/Effects/footprints.rsi");
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
}
