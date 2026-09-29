using Content.Shared.Access;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.TamperSeal.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true, fieldDeltas: true)]
public sealed partial class TamperSealComponent : Component
{
    #region Parties

    /// <summary>
    /// The deliverer of this tamper-sealed container.
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<CargoAccountPrototype> Deliverer = "Cargo";

    /// <summary>
    /// The recipient of this tamper-sealed container, if any.
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<CargoAccountPrototype> Recipient = "Cargo";

    #endregion
    #region Derivatives

    /*
     * All properties in this section are derived from the Recipient but can be manually overridden,
     * be it for mapping or event purposes.
     */

    /// <summary>
    /// The name of the recipient of the sealed container.
    /// </summary>
    [DataField, AutoNetworkedField] public LocId RecipientName = "tamper-seal-account-name-unknown";

    /// <summary>
    /// The color of the recipient in the Examine text. This differs from the seal color and is usually a lower contrast.
    /// </summary>
    [DataField, AutoNetworkedField] public Color RecipientExamineColor = Color.White;

    /// <summary>
    /// The color of the tamper seal sprite.
    /// </summary>
    [DataField, AutoNetworkedField] public Color Color = Color.White; // Better than transparent as default.

    /// <summary>
    /// The access levels that can unlock this tamper seal legally.
    /// </summary>
    [DataField, AutoNetworkedField] public List<TamperSealAccessPattern> Accesses = new();

    #endregion
    #region State

    /// <summary>
    /// Whether the tamper seal was opened. Does not distinguish between unsealing and destroying the seal.
    /// </summary>
    [DataField, AutoNetworkedField] public bool Opened;

    /// <summary>
    /// Whether the tamper seal was destroyed.
    /// </summary>
    [DataField, AutoNetworkedField] public bool Destroyed;

    #endregion
    #region Unsealing

    /// <summary>
    /// The amount of time in seconds it takes to unseal the tamper seal normally (with access).
    /// </summary>
    [DataField, AutoNetworkedField] public float UnsealTime = .75f;

    /// <summary>
    /// The sound to play when the Unseal do-after begins.
    /// </summary>
    [DataField, AutoNetworkedField] public SoundSpecifier UnsealBeginSound =
        new SoundPathSpecifier("/Audio/Items/Handcuffs/rope_takeoff.ogg");

    /// <summary>
    /// The sound to play when the Unseal do-after ends.
    /// </summary>
    [DataField, AutoNetworkedField] public SoundSpecifier UnsealEndSound =
        new SoundPathSpecifier("/Audio/Items/Handcuffs/rope_breakout.ogg");

    #endregion
    #region Destroying

    /// <summary>
    /// Tool quality needed to undo the tamper seal.
    /// </summary>
    [DataField, AutoNetworkedField] public HashSet<ProtoId<ToolQualityPrototype>> DestroyToolQualities =
        new() { "Slicing", "Cutting" };

    /// <summary>
    /// How long it takes to Destroy the seal with the correct tool.
    /// </summary>
    [DataField, AutoNetworkedField] public float DestroyWithToolTime = 1.0f;

    /// <summary>
    /// How long it takes to Destroy the seal with bare hands / an incorrect tool.
    /// </summary>
    [DataField, AutoNetworkedField] public float DestroyWithHandsTime = 5.0f;

    /// <summary>
    /// The sound to play when the Destroy do-after begins.
    /// </summary>
    [DataField, AutoNetworkedField] public SoundSpecifier DestroyBeginSound =
        new SoundPathSpecifier("/Audio/Items/Handcuffs/rope_takeoff.ogg");

    /// <summary>
    /// The sound to play when the Destroy do-after ends.
    /// </summary>
    [DataField, AutoNetworkedField] public SoundSpecifier DestroyEndSound =
        new SoundPathSpecifier("/Audio/Items/Handcuffs/rope_breakout.ogg");

    #endregion
}

[Serializable, NetSerializable, DataRecord]
public partial record struct TamperSealAccessPattern(
    HashSet<ProtoId<AccessLevelPrototype>>? AllOf = null,
    HashSet<ProtoId<AccessLevelPrototype>>? NoneOf = null);

/// <summary>
/// These are basically flags that are networked so the visualizer knows how to render it.
/// </summary>
[Serializable, NetSerializable]
public enum TamperSealVisuals : byte
{
    Opened,
    Destroyed
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
    Destroyed,
}
