using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

#region Starlight
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Radio;
using Content.Shared._Starlight.Speech;
using static Content.Shared.Administration.Notes.AdminMessageEuiState;
#endregion Starlight

namespace Content.Shared.Chat;

/// <summary>
/// This event should be sent everytime an entity talks (Radio, local chat, etc...).
/// The event is sent to both the entity itself, and all clothing (For stuff like voice masks).
/// </summary>
public sealed class TransformSpeakerNameEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid Sender;
    public string VoiceName;
    public ProtoId<SpeechVerbPrototype>? SpeechVerb;

    public TransformSpeakerNameEvent(EntityUid sender, string name)
    {
        Sender = sender;
        VoiceName = name;
        SpeechVerb = null;
    }
}

/// <summary>
/// Raised broadcast in order to transform speech.transmit
/// </summary>
public sealed class TransformSpeechEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid Sender;
    public _Starlight.Speech.SpeechMessage Message; // Starlight

    public TransformSpeechEvent(EntityUid sender, _Starlight.Speech.SpeechMessage message) // Starlight
    {
        Sender = sender;
        Message = message; // Starlight
    }
}

public sealed class CheckIgnoreSpeechBlockerEvent : EntityEventArgs
{
    public EntityUid Sender;
    public bool IgnoreBlocker;

    public CheckIgnoreSpeechBlockerEvent(EntityUid sender, bool ignoreBlocker)
    {
        Sender = sender;
        IgnoreBlocker = ignoreBlocker;
    }
}

/// <summary>
/// Raised on an entity when it speaks, either through 'say' or 'whisper'.
/// </summary>
public sealed class EntitySpokeEvent : EntityEventArgs
{
    public readonly EntityUid Source;
    public readonly SpeechMessage Message; // Starlight
    public readonly string? ObfuscatedMessage; // not null if this was a whisper
    public readonly bool IsWhisper; // Starlight
    public readonly LanguagePrototype Language; // Starlight

    /// <summary>
    /// If the entity was trying to speak into a radio, this was the channel they were trying to access. If a radio
    /// message gets sent on this channel, this should be set to null to prevent duplicate messages.
    /// </summary>
    public RadioChannelPrototype? Channel;

    //Starlight beign
    /// <summary>
    /// Whether to switch to custom channel system or not.
    /// </summary>
    public bool UsingCustomChannel;

    /// <summary>
    /// If the entity was trying to speak into a radio, this was the custom channel they were trying to access.
    /// </summary>
    public CustomRadioChannelData? CustomChannel;

    public EntitySpokeEvent(EntityUid source, SpeechMessage message, string? obfuscatedMessage, bool isWhisper, LanguagePrototype language, CustomRadioChannelData customChannel)
    {
        Source = source;
        Message = message;
        CustomChannel = customChannel;
        UsingCustomChannel = true;
        ObfuscatedMessage = obfuscatedMessage;
        IsWhisper = isWhisper;
        Language = language;
    }
    //Starlight end

    public EntitySpokeEvent(EntityUid source, SpeechMessage message, RadioChannelPrototype? channel, string? obfuscatedMessage, bool isWhisper, LanguagePrototype language) // Starlight - added isWhisper, language, ttsMessage
    {
        Source = source;
        Message = message;
        Channel = channel;
        ObfuscatedMessage = obfuscatedMessage;
        IsWhisper = isWhisper; // Starlight
        Language = language; // Starlight
    }
}
