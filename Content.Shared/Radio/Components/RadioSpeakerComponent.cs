using Content.Shared.Radio.EntitySystems;
using Content.Shared.Chat;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._Goobstation.StationRadio.Systems; // Goobstation - Station Radio

namespace Content.Shared.Radio.Components;

/// <summary>
///     Listens for radio messages and relays them to local chat.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRadioDeviceSystem), typeof(StationRadioReceiverSystem))] // Goobstation - Add StationRadioReceiverSystem access.
public sealed partial class RadioSpeakerComponent : Component
{
    /// <summary>
    /// Whether or not interacting with this entity
    /// toggles it on or off.
    /// </summary>
    [DataField]
    public bool ToggleOnInteract = true;

    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = new() { SharedChatSystem.CommonChannel };

    [DataField, AutoNetworkedField]
    public bool Enabled;

    // Goobstation - Radio Host
    /// <summary>
    /// Whether or not a message is parsed through the radio when when sent in local chat.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ParseRadioPrefix;

    /// <summary>
    /// Does the radio need to be on a power grid to work?
    /// </summary>
    [DataField]
    public bool PowerRequired;
    // Goobstation - End - Radio Host
}
