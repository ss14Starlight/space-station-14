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
    /// <summary>
    /// The color of the tamper seal.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Color Color = Color.White; // Better than invisible as default.

    /// <summary>
    /// Whether the tamper seal was opened.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool Opened;

    [DataField] public SoundCollectionSpecifier UnsealBeginSound = new("CargoTamperSealUndoBegin"); // Same as destroy
    [DataField] public SoundCollectionSpecifier UnsealEndSound = new("CargoTamperSealUndoEnd"); // Same as destroy

    [DataField]
    [AutoNetworkedField]
    public float UnsealTime = .75f;

    /// <summary>
    /// Whether the tamper seal was destroyed.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool Destroyed;

    /// <summary>
    /// Tool capability needed to undo the tamper seal.
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<ToolQualityPrototype> DestroyToolQuality = "Slicing";

    [DataField, AutoNetworkedField] public float DestroyWithToolTime = .8f;
    [DataField, AutoNetworkedField] public float DestroyWithHandsTime = 5.0f;

    [DataField] public SoundCollectionSpecifier DestroyBeginSound = new("CargoTamperSealUndoBegin");
    [DataField] public SoundCollectionSpecifier DestroyEndSound = new("CargoTamperSealUndoEnd");

    /// <summary>
    /// The access levels that can unlock this tamper seal legally.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public HashSet<ProtoId<AccessLevelPrototype>> Accesses = new();

    #region Rewards and Penalties

    [DataField] [AutoNetworkedField] public EntityUid RecipientStation = EntityUid.Invalid;
    [DataField] [AutoNetworkedField] public ProtoId<CargoAccountPrototype> DelivererAccount = "Cargo";
    [DataField] [AutoNetworkedField] public ProtoId<CargoAccountPrototype> RecipientAccount = "Cargo";

    [DataField] [AutoNetworkedField] public int RewardSpesos = 500;

    /// <summary>
    /// How much the DelivererAccount is penalized on failed delivery.
    /// </summary>
    [DataField] [AutoNetworkedField] public int PenaltySpesos = 250;

    /// <summary>
    /// How much the RecipientAccount is refunded on failed delivery.
    /// </summary>
    [DataField] [AutoNetworkedField] public int PenaltyRefundSpesos = 500;

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
