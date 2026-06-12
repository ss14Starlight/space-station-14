using Content.Shared.Access;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.TamperSeal.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class TamperSealComponent : Component
{
    /// <summary>
    /// Whether the tamper seal was opened.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool Opened;

    /// <summary>
    /// Whether the tamper seal was violated.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool Violated;

    /// <summary>
    /// The color of the tamper seal.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Color Color;

    /// <summary>
    /// The access levels that can unlock this tamper seal legally.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public HashSet<ProtoId<AccessLevelPrototype>> Accesses = new();

    /// <summary>
    /// Tool capability needed to undo the tamper seal.
    /// </summary>
    [DataField] public ProtoId<ToolQualityPrototype> ViolateTool = "Slicing";
}

/// <summary>
/// These are basically flags that are networked so the visualizer knows how to render it.
/// </summary>
[Serializable, NetSerializable]
public enum TamperSealVisuals : byte
{
    Opened,
    Violated
}

/// <summary>
/// Visual layers that are rendered client-side. The visualizer enables/disables these based on the visual flags.
/// </summary>
[Serializable, NetSerializable]
public enum TamperSealLayers : byte
{
    Base,
    Sealed,
    Opened,
    Violated,
}
