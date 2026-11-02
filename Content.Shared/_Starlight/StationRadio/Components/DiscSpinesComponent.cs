using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StationRadio.Components;

/// <summary>
/// Shows the discs kept in a container as spines in the colour of their covers.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DiscSpinesComponent : Component
{
    [DataField(required: true)]
    public string Container = string.Empty;

    /// <summary>
    /// How many spine layers the sprite has.
    /// </summary>
    [DataField]
    public int Count = 8;

    /// <summary>
    /// Spine colour of a disc kept without a cover: records are black at the edge, CDs silver.
    /// </summary>
    [DataField]
    public Color BareColor = Color.FromHex("#1a1b1f");
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class DiscSpinesData
{
    [DataField]
    public Color[] Colors = [];
}

[Serializable, NetSerializable]
public enum DiscSpinesVisuals : byte
{
    Spines,
}
