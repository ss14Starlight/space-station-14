using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cargo.TamperSeal.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TamperSealValueComponent : Component
{
    /// <summary>
    /// The entity ID of the station that all referenced CargoAccounts belong to.
    /// </summary>
    [DataField, AutoNetworkedField] public EntityUid StationId;

    /// <summary>
    /// The value represented by this tamper seal. Only used for performance tracking.
    /// Note that this represents the value of a single crate and not an entire order!
    /// </summary>
    [DataField, AutoNetworkedField] public int Value;

    [DataField, AutoNetworkedField] public FinancialMutation Reward;
    [DataField, AutoNetworkedField] public FinancialMutation Penalty;
    [DataField, AutoNetworkedField] public FinancialMutation? Refund;

    /// <summary>
    /// The sound that plays when a reward is given.
    /// </summary>
    [DataField] public SoundSpecifier RewardSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    /// <summary>
    /// The sound that plays when a penalty is incurred.
    /// </summary>
    [DataField] public SoundSpecifier PenaltySound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg")
    {
        Params = new AudioParams
        {
            Volume = -3 // This sound is so incredibly loud.
        }
    };

}

[Serializable, NetSerializable]
public record struct FinancialMutation(ProtoId<CargoAccountPrototype> Account, int Amount);
