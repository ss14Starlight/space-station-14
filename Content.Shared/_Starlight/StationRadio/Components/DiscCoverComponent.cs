using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DiscCoverComponent : Component
{
    /// <summary>
    /// Container holding the disc.
    /// </summary>
    [DataField]
    public string Slot = "vinyl";

    /// <summary>
    /// Sprite state prefix of the cover patterns, followed by the pattern number.
    /// </summary>
    [DataField]
    public string MotifPrefix = "sleeve-motif-";

    /// <summary>
    /// How many patterns the sprite has.
    /// </summary>
    [DataField]
    public int Patterns = 12;

    /// <summary>
    /// Whether an empty cover still shows its base, in <see cref="EmptyColor"/>; a clear CD case shows nothing.
    /// </summary>
    [DataField]
    public bool ShowEmptyBase = true;

    /// <summary>
    /// Colour of an empty cover, like plain cardboard.
    /// </summary>
    [DataField]
    public Color EmptyColor = Color.FromHex("#c8b78e");
}

[Serializable, NetSerializable]
public enum DiscCoverVisuals : byte
{
    Disc, // Disc inside

    Pattern, // Disc pattern
    Main, // Disc main color
    Accent, // Disc secondary color
}

[Serializable, NetSerializable]
public enum DiscCoverVisualLayers : byte
{
    Base,
    Motif,
    Peek,
}
