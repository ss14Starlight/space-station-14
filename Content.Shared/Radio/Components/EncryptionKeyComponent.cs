using Content.Shared.Chat;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility; // Starlight
using Content.Shared._Starlight.Radio; // Starlight

namespace Content.Shared.Radio.Components;

/// <summary>
///     This component is currently used for providing access to channels for "HeadsetComponent"s.
///     It should be used for intercoms and other radios in future.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)] // Starlight edit
public sealed partial class EncryptionKeyComponent : Component, ISupportsCustomChannels // Starlight edit
{
    [DataField, AutoNetworkedField] // Starlight edit
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = new();

    /// <summary>
    ///     This is the channel that will be used when using the default/department prefix (<see cref="SharedChatSystem.DefaultChannelKey"/>).
    /// </summary>
    [DataField, AutoNetworkedField] // Starlight edit
    public string? DefaultChannel; // Starlight edit | Use string to support custom channels
    
    //Starlight begin
    /// <summary>
    /// Set of custom channel data
    /// </summary>
    [DataField, AutoNetworkedField] public HashSet<CustomRadioChannelData> CustomChannels { get; set; } = [];

    [DataField, ViewVariables(VVAccess.ReadOnly)] public Vector2i ExpectedSpriteSize = new(32, 32);

    // These really have no purpose being data fields, its just it won't serialize otherwise. 
    [DataField, AutoNetworkedField] public string? CustomBaseRsi;
    [DataField, AutoNetworkedField] public string? CustomIconRsi;
    [DataField, AutoNetworkedField] public string? CustomBaseState;
    [DataField, AutoNetworkedField] public string? CustomIconState;
    //Starlight end
}
