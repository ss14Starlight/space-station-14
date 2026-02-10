using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Radio; // Starlight

namespace Content.Shared.Radio.Components;

/// <summary>
///     This component is required to receive radio message events.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // Starlight edit
public sealed partial class ActiveRadioComponent : Component, ISupportsCustomChannels // Starlight edit
{
    /// <summary>
    ///     The channels that this radio is listening on.
    /// </summary>
    [DataField, AutoNetworkedField] //Starlight edit
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = new();
    
    //Starlight begin
    /// <summary>
    /// The custom channels that this radio is listening on.
    /// </summary>
    [DataField, AutoNetworkedField] public HashSet<CustomRadioChannelData> CustomChannels { get; set; } = [];
    //Starlight end

    /// <summary>
    /// A toggle for globally receiving all radio channels.
    /// Overrides <see cref="Channels"/>
    /// </summary>
    [DataField]
    public bool ReceiveAllChannels;

    /// <summary>
    ///     If this radio can hear all messages on all maps
    /// </summary>
    [DataField]
    public bool GlobalReceive = false;
}
