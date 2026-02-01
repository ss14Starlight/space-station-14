using Content.Shared.Chat;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Radio; // Starlight

namespace Content.Shared.Radio.Components;

/// <summary>
///     This component is by entities that can contain encryption keys
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // Starlight edit
public sealed partial class EncryptionKeyHolderComponent : Component, ISupportsCustomChannels // Starlight edit
{
    /// <summary>
    ///     Whether or not encryption keys can be removed from the headset.
    /// </summary>
    [DataField]
    public bool KeysUnlocked = true;

    /// <summary>
    ///     The tool required to extract the encryption keys from the headset.
    /// </summary>
    [DataField]
    public ProtoId<ToolQualityPrototype> KeysExtractionMethod = "Screwing";

    [DataField]
    public int KeySlots = 2;

    [DataField]
    public SoundSpecifier KeyExtractionSound = new SoundPathSpecifier("/Audio/Items/pistol_magout.ogg");

    [DataField]
    public SoundSpecifier KeyInsertionSound = new SoundPathSpecifier("/Audio/Items/pistol_magin.ogg");

    [ViewVariables]
    public Container KeyContainer = default!;
    public const string KeyContainerName = "key_slots";

    /// <summary>
    ///     Combined set of radio channels provided by all contained keys.
    /// </summary>
    [ViewVariables, AutoNetworkedField] // Starlight edit
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = new();
    
    //Starlight begin
    /// <summary>
    /// Combined set of custom channels provided by all contained keys.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public HashSet<CustomRadioChannelData> CustomChannels { get; set; } = [];
    //Starlight end

    /// <summary>
    ///     This is the channel that will be used when using the default/department prefix (<see cref="SharedChatSystem.DefaultChannelKey"/>).
    /// </summary>
    [ViewVariables, AutoNetworkedField] // Starlight edit
    public string? DefaultChannel;

    #region Starlight
    [DataField]
    public bool CanBeExamined = true;
    #endregion Starlight
}
