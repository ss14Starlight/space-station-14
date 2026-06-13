using Content.Shared.Access;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.TamperSeal.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class TamperSealComponent : Component
{
    #region State
    /// <summary>
    /// The color of the tamper seal.
    /// </summary>
    [DataField, AutoNetworkedField] public Color Color = Color.White; // Better than invisible as default.

    /// <summary>
    /// Whether the tamper seal was opened. Does not distinguish between unsealing and destroying the seal.
    /// </summary>
    [DataField, AutoNetworkedField] public bool Opened;

    /// <summary>
    /// Whether the tamper seal was destroyed.
    /// </summary>
    [DataField, AutoNetworkedField] public bool Destroyed;

    /// <summary>
    /// The access levels that can unlock this tamper seal legally.
    /// </summary>
    [DataField, AutoNetworkedField] public HashSet<ProtoId<AccessLevelPrototype>> Accesses = new();

    #endregion
    #region Unsealing

    /// <summary>
    /// The amount of time in seconds it takes to unseal the tamper seal normally (with access).
    /// </summary>
    [DataField, AutoNetworkedField] public float UnsealTime = .75f;

    /// <summary>
    /// The sound to play when the Unseal do-after begins.
    /// </summary>
    [DataField, AutoNetworkedField] public SoundCollectionSpecifier UnsealBeginSound = new("CargoTamperSealUndoBegin");

    /// <summary>
    /// The sound to play when the Unseal do-after ends.
    /// </summary>
    [DataField, AutoNetworkedField] public SoundCollectionSpecifier UnsealEndSound = new("CargoTamperSealUndoEnd"); // Same as destroy

    #endregion
    #region Destroying

    /// <summary>
    /// Tool capability needed to undo the tamper seal.
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<ToolQualityPrototype> DestroyToolQuality = "Slicing";

    /// <summary>
    /// How long it takes to Destroy the seal with the correct tool.
    /// </summary>
    [DataField, AutoNetworkedField] public float DestroyWithToolTime = .8f;

    /// <summary>
    /// How long it takes to Destroy the seal with bare hands / an incorrect tool.
    /// </summary>
    [DataField, AutoNetworkedField] public float DestroyWithHandsTime = 5.0f;

    /// <summary>
    /// The sound to play when the Destroy do-after begins.
    /// </summary>
    [DataField, AutoNetworkedField] public SoundCollectionSpecifier DestroyBeginSound = new("CargoTamperSealUndoBegin");
    /// <summary>
    /// The sound to play when the Destroy do-after ends.
    /// </summary>
    [DataField, AutoNetworkedField] public SoundCollectionSpecifier DestroyEndSound = new("CargoTamperSealUndoEnd");

    #endregion
    #region Rewards and Penalties

    /// <summary>
    /// The entity ID of the station that all referenced CargoAccount instances belong to.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid RecipientStation = EntityUid.Invalid;

    /// <summary>
    /// The ID of the account responsible for a successful delivery.
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<CargoAccountPrototype> DelivererAccount = "Cargo";

    /// <summary>
    /// The ID of the account that placed the order, and that should be reimbursed on a failed delivery.
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<CargoAccountPrototype> RecipientAccount = "Cargo";

    /// <summary>
    /// The reward to be given to <see cref="DelivererAccount"/> on a successful delivery.
    /// </summary>
    [DataField, AutoNetworkedField] public int RewardSpesos;

    /// <summary>
    /// How much the <see cref="DelivererAccount"/> is penalized on failed delivery.
    /// </summary>
    [DataField] [AutoNetworkedField] public int PenaltySpesos;

    /// <summary>
    /// How much the <see cref="RecipientAccount"/> is refunded on failed delivery.
    /// </summary>
    [DataField, AutoNetworkedField] public int PenaltyRefundSpesos;

    #endregion

}

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
