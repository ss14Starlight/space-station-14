using Content.Shared._Starlight.Radio;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Implants.Components;

/// <summary>
/// Gives the user access to a given channel without the need for a headset.
/// </summary>
[RegisterComponent, AutoGenerateComponentState] // Starlight edit
public sealed partial class RadioImplantComponent : Component, ISupportsCustomChannels // Starlight edit
{
    /// <summary>
    /// The radio channel(s) to grant access to.
    /// </summary>
    [DataField(required: true), AutoNetworkedField] // Starlight edit
    public HashSet<ProtoId<RadioChannelPrototype>> RadioChannels = new();

    /// <summary>
    /// The radio channels that have been added by the implant to a user's ActiveRadioComponent.
    /// Used to track which channels were successfully added (not already in user)
    /// </summary>
    /// <remarks>
    /// Should not be modified outside RadioImplantSystem.cs
    /// </remarks>
    [DataField, AutoNetworkedField] // Starlight edit
    public HashSet<ProtoId<RadioChannelPrototype>> ActiveAddedChannels = new();

    /// <summary>
    /// The radio channels that have been added by the implant to a user's IntrinsicRadioTransmitterComponent.
    /// Used to track which channels were successfully added (not already in user)
    /// </summary>
    /// <remarks>
    /// Should not be modified outside RadioImplantSystem.cs
    /// </remarks>
    [DataField, AutoNetworkedField] // Starlight edit
    public HashSet<ProtoId<RadioChannelPrototype>> TransmitterAddedChannels = new();
    
    //Starlight begin
    [ViewVariables, AutoNetworkedField] public HashSet<CustomRadioChannelData> CustomChannels { get; set; } = [];
    [ViewVariables, AutoNetworkedField] public HashSet<CustomRadioChannelData> ActiveAddedCustomRadioChannels = [];
    [ViewVariables, AutoNetworkedField] public HashSet<CustomRadioChannelData> TransmitterAddedCustomRadioChannels = [];
    //Starlight end
}
