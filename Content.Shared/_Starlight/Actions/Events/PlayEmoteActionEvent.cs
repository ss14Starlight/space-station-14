using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Events;

/// <summary>
/// Raised when an emote quick-bind action is performed. Triggers the bound emote on the performer.
/// </summary>
public sealed partial class PlayEmoteActionEvent : InstantActionEvent
{
    /// <summary>
    /// Contains the prototype ID of the emote to be played.
    /// </summary>
    [DataField]
    public ProtoId<EmotePrototype> ProtoId;
}
